package whatsapp

import (
	"encoding/json"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
)

func TestWebhookChallenge(t *testing.T) {
	ch, err := VerifyWebhookChallenge(map[string]string{
		"hub.mode": "subscribe", "hub.verify_token": "segredo", "hub.challenge": "12345",
	}, "segredo")
	if err != nil || ch != "12345" {
		t.Fatalf("%s %v", ch, err)
	}
	_, err = VerifyWebhookChallenge(map[string]string{
		"hub.mode": "subscribe", "hub.verify_token": "x", "hub.challenge": "1",
	}, "segredo")
	if _, ok := err.(*ValidationError); !ok {
		t.Fatalf("%v", err)
	}
}

func TestMetaSignature(t *testing.T) {
	body := `{"object":"whatsapp_business_account"}`
	header := MetaSignatureHeader(body, "app_secret_exemplo")
	ok, err := VerifyMetaSignature(body, header, "app_secret_exemplo")
	if err != nil || !ok {
		t.Fatal(err)
	}
	ok, _ = VerifyMetaSignature(body, header, "outro")
	if ok {
		t.Fatal("deveria falhar")
	}
	if err := VerifyMetaSignatureOrThrow("{}", header, "app_secret_exemplo"); err == nil {
		t.Fatal("esperado SignatureError")
	}
}

func TestParseInbound(t *testing.T) {
	payload := map[string]any{
		"entry": []any{
			map[string]any{
				"changes": []any{
					map[string]any{
						"value": map[string]any{
							"messages": []any{
								map[string]any{"from": "5511", "id": "wamid.1", "timestamp": "1", "type": "text", "text": map[string]any{"body": "pix"}},
							},
						},
					},
				},
			},
		},
	}
	msgs := ParseInboundMessages(payload)
	if len(msgs) != 1 || msgs[0].Text != "pix" || msgs[0].From != "5511" {
		t.Fatalf("%+v", msgs)
	}
}

func TestEntitlementAddons(t *testing.T) {
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if !strings.Contains(r.URL.Path, "/addons/whatsapp/entitlement") {
			t.Fatalf("path %s", r.URL.Path)
		}
		w.Header().Set("Content-Type", "application/json")
		_ = json.NewEncoder(w).Encode(map[string]any{"data": map[string]any{"enabled": true, "plan": "saas"}})
	}))
	defer srv.Close()
	checker := NewOrdexPayEntitlementChecker("chave", srv.URL, srv.Client())
	result, err := checker.Check("acme")
	if err != nil || !result.Enabled || result.Plan != "saas" {
		t.Fatalf("%+v %v", result, err)
	}
}

func TestEntitlementFallbackLicense(t *testing.T) {
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		if strings.HasSuffix(r.URL.Path, "/addons/whatsapp/entitlement") {
			w.WriteHeader(http.StatusNotFound)
			_, _ = io.WriteString(w, `{"message":"not found"}`)
			return
		}
		_ = json.NewEncoder(w).Encode(map[string]any{
			"success": true,
			"data":    map[string]any{"valid": true, "addons": map[string]any{"whatsapp": map[string]any{"enabled": true}}},
		})
	}))
	defer srv.Close()
	checker := NewOrdexPayEntitlementChecker("chave", srv.URL, srv.Client())
	result, err := checker.Check("acme")
	if err != nil || !result.Enabled {
		t.Fatalf("%+v %v", result, err)
	}
}

func TestStaticEntitlement(t *testing.T) {
	on, _ := (StaticEntitlementChecker{Enabled: true}).Check("t")
	off, _ := (StaticEntitlementChecker{Enabled: false}).Check("t")
	if !on.Enabled || off.Enabled || on.AddOn != "whatsapp" {
		t.Fatal(on, off)
	}
}
