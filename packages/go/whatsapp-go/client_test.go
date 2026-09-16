package whatsapp

import (
	"encoding/json"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"sync"
	"testing"
	"time"
)

type captured struct {
	Method string
	Path   string
	Auth   string
	Body   string
}

func graphServer(t *testing.T, status int, payload any, calls *[]captured) *httptest.Server {
	t.Helper()
	return httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		b, _ := io.ReadAll(r.Body)
		*calls = append(*calls, captured{Method: r.Method, Path: r.URL.Path, Auth: r.Header.Get("Authorization"), Body: string(b)})
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(status)
		_ = json.NewEncoder(w).Encode(payload)
	}))
}

var metaOK = map[string]any{
	"messaging_product": "whatsapp",
	"contacts":          []any{map[string]any{"input": "5511999999999", "wa_id": "5511999999999"}},
	"messages":          []any{map[string]any{"id": "wamid.TEST"}},
}

func TestClientConfigIncompleta(t *testing.T) {
	_, err := NewClient(Options{})
	if err == nil || !strings.Contains(err.Error(), "AccessToken") {
		t.Fatalf("%v", err)
	}
}

func TestClientBloqueiaSemAddon(t *testing.T) {
	var calls []captured
	srv := graphServer(t, 200, metaOK, &calls)
	defer srv.Close()
	client, err := NewClient(Options{
		AccessToken:   "token",
		PhoneNumberID: "123",
		TenantID:      "acme",
		Entitlement:   StaticEntitlementChecker{Enabled: false, Message: "addon off"},
		GraphBaseURL:  srv.URL,
		HTTPClient:    srv.Client(),
	})
	if err != nil {
		t.Fatal(err)
	}
	_, err = client.SendText("5511", "oi", CategoryService, "")
	if _, ok := err.(*EntitlementError); !ok {
		t.Fatalf("%v", err)
	}
}

func TestSendTemplateUtility(t *testing.T) {
	var calls []captured
	srv := graphServer(t, 200, metaOK, &calls)
	defer srv.Close()
	client, err := NewClient(Options{
		AccessToken:   "token",
		PhoneNumberID: "555",
		TenantID:      "acme",
		Entitlement:   StaticEntitlementChecker{Enabled: true},
		Clock:         func() time.Time { return time.Date(2026, 9, 16, 15, 0, 0, 0, time.UTC) },
		GraphBaseURL:  srv.URL,
		GraphVersion:  "v21.0",
		HTTPClient:    srv.Client(),
	})
	if err != nil {
		t.Fatal(err)
	}
	result, err := client.SendTemplate("5511999999999", CategoryUtility, TemplatePayload{Name: "pix_recebido", Language: "pt_BR"}, "")
	if err != nil {
		t.Fatal(err)
	}
	if result.Category != CategoryUtility || result.Meta.Messages[0].ID != "wamid.TEST" {
		t.Fatalf("%+v", result)
	}
	if result.Metering.Used != 1 || result.CobrancaPlan.TotalQuantity != 1 {
		t.Fatalf("metering %+v plan %+v", result.Metering, result.CobrancaPlan)
	}
	if len(calls) != 1 || calls[0].Method != http.MethodPost {
		t.Fatalf("%+v", calls)
	}
	if !strings.HasSuffix(calls[0].Path, "/v21.0/555/messages") {
		t.Fatalf("path %s", calls[0].Path)
	}
	if calls[0].Auth != "Bearer token" {
		t.Fatal(calls[0].Auth)
	}
	var body map[string]any
	_ = json.Unmarshal([]byte(calls[0].Body), &body)
	if body["type"] != "template" {
		t.Fatalf("%v", body)
	}
	tpl := body["template"].(map[string]any)
	if tpl["name"] != "pix_recebido" {
		t.Fatal(tpl)
	}
	var cb map[string]any
	_ = json.Unmarshal([]byte(body["biz_opaque_callback_data"].(string)), &cb)
	if cb["category"] != "utility" {
		t.Fatal(cb)
	}
}

func TestRollbackSeGraphFalhar(t *testing.T) {
	var calls []captured
	srv := graphServer(t, 400, map[string]any{"error": map[string]any{"message": "invalid to"}}, &calls)
	defer srv.Close()
	store := NewInMemoryUsageStore()
	client, err := NewClient(Options{
		AccessToken:   "token",
		PhoneNumberID: "555",
		TenantID:      "acme",
		Entitlement:   StaticEntitlementChecker{Enabled: true},
		Store:         store,
		GraphBaseURL:  srv.URL,
		HTTPClient:    srv.Client(),
	})
	if err != nil {
		t.Fatal(err)
	}
	_, err = client.SendText("1", "oi", CategoryService, "")
	if err == nil {
		t.Fatal("esperado erro")
	}
	snap, _ := store.Get("acme", client.Metering.Period())
	if snap.Counts.Service != 0 {
		t.Fatalf("nao fez rollback: %d", snap.Counts.Service)
	}
}

func TestHardNaoChamaMeta(t *testing.T) {
	var calls []captured
	srv := graphServer(t, 200, metaOK, &calls)
	defer srv.Close()
	quota := int64(1)
	client, err := NewClient(Options{
		AccessToken:   "token",
		PhoneNumberID: "555",
		TenantID:      "acme",
		Entitlement:   StaticEntitlementChecker{Enabled: true},
		QuotaMode:     QuotaHard,
		Plan: &PlanOverride{Categories: map[MessageCategory]CategoryQuotaPatch{
			CategoryService: {MonthlyQuota: &quota},
		}},
		GraphBaseURL: srv.URL,
		HTTPClient:   srv.Client(),
	})
	if err != nil {
		t.Fatal(err)
	}
	if _, err := client.SendText("5511", "1", CategoryService, ""); err != nil {
		t.Fatal(err)
	}
	_, err = client.SendText("5511", "2", CategoryService, "")
	if _, ok := err.(*QuotaExceededError); !ok {
		t.Fatalf("%v", err)
	}
	if len(calls) != 1 {
		t.Fatalf("graph calls %d", len(calls))
	}
}

type memoryAgenda struct {
	mu    sync.Mutex
	items map[string]CobrancaRecord
}

func newMemoryAgenda() *memoryAgenda {
	return &memoryAgenda{items: map[string]CobrancaRecord{}}
}

func (m *memoryAgenda) CreateCobranca(in CreateCobrancaInput) (CobrancaRecord, error) {
	m.mu.Lock()
	defer m.mu.Unlock()
	rec := CobrancaRecord{
		ID: "cob-" + in.ExternalReference, ExternalReference: in.ExternalReference,
		ValorCentavos: in.ValorCentavos, Vencimento: in.Vencimento, Status: "pendente", Pagador: &in.Pagador,
	}
	m.items[rec.ID] = rec
	return rec, nil
}

func (m *memoryAgenda) ListCobrancas(in ListCobrancasInput) ([]CobrancaRecord, error) {
	m.mu.Lock()
	defer m.mu.Unlock()
	var out []CobrancaRecord
	for _, c := range m.items {
		if in.ExternalReference != "" && c.ExternalReference != in.ExternalReference {
			continue
		}
		out = append(out, c)
	}
	return out, nil
}

func (m *memoryAgenda) CancelCobranca(id string) (CobrancaRecord, error) {
	m.mu.Lock()
	defer m.mu.Unlock()
	rec := m.items[id]
	rec.Status = "cancelada"
	m.items[id] = rec
	return rec, nil
}

func TestSyncCriaECancela(t *testing.T) {
	agenda := newMemoryAgenda()
	ctrl := NewCobrancaQuantityController(agenda, Pagador{Documento: "12345678901", Nome: "Empresa SaaS"}, 10)
	full := ComputeCobrancaQuantity(UsageCounts{Auth: 20_000}, CenarioBasePlan, ConversionOptions{
		Period: "2026-09", TenantID: "acme", Strategy: StrategyBySessions, SessionsPerCobranca: 10_000,
	})
	if full.TotalQuantity != 2 {
		t.Fatalf("qty %d", full.TotalQuantity)
	}
	first, err := ctrl.Sync(full)
	if err != nil {
		t.Fatal(err)
	}
	if first.Created != 2 || first.Cancelled != 0 {
		t.Fatalf("%+v", first)
	}
	if first.Items[0].Cobranca.Vencimento != "2026-09-10" {
		t.Fatal(first.Items[0].Cobranca.Vencimento)
	}
	reduced := ComputeCobrancaQuantity(UsageCounts{Auth: 10_000}, CenarioBasePlan, ConversionOptions{
		Period: "2026-09", TenantID: "acme", Strategy: StrategyBySessions, SessionsPerCobranca: 10_000,
	})
	second, err := ctrl.Sync(reduced)
	if err != nil {
		t.Fatal(err)
	}
	if second.Kept != 1 || second.Cancelled != 1 || second.Created != 0 {
		t.Fatalf("%+v", second)
	}
	pending := 0
	var ref string
	for _, c := range agenda.items {
		if c.Status == "pendente" {
			pending++
			ref = c.ExternalReference
		}
	}
	if pending != 1 || ref != "wa:acme:2026-09:auth:1" {
		t.Fatalf("pending %d ref %s", pending, ref)
	}
}
