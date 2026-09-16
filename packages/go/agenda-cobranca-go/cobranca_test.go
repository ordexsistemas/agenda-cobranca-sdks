package agendacobranca

import (
	"context"
	"encoding/json"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"
)

func TestCreateCobrancaEnviaApiKeySemHMAC(t *testing.T) {
	var capturado *http.Request

	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		capturado = r.Clone(r.Context())
		_, _ = io.ReadAll(r.Body)
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusCreated)
		_ = json.NewEncoder(w).Encode(map[string]any{
			"data": map[string]any{
				"id":                 "11111111-1111-4111-8111-111111111111",
				"external_reference": "pedido-1",
				"valor_centavos":     15000,
				"vencimento":         "2026-10-01",
				"status":             "pendente",
				"pagador": map[string]any{
					"documento": "12345678901",
					"nome":      "Maria Silva",
					"email":     "maria@example.com",
				},
			},
		})
	}))
	defer srv.Close()

	client, err := NewClient(Options{
		APIKey:  "api_key_exemplo",
		BaseURL: srv.URL + "/v2/externo",
	})
	if err != nil {
		t.Fatal(err)
	}

	cobranca, err := client.CreateCobranca(context.Background(), CreateCobrancaInput{
		ExternalReference: "pedido-1",
		ValorCentavos:     15000,
		Vencimento:        "2026-10-01",
		Pagador:           Pagador{Documento: "12345678901", Nome: "Maria Silva", Email: "maria@example.com"},
		IdempotencyKey:    "idem-1",
	})
	if err != nil {
		t.Fatalf("create: %v", err)
	}
	if cobranca.ID == "" || cobranca.Status != "pendente" {
		t.Fatalf("cobranca inesperada: %+v", cobranca)
	}

	if capturado == nil {
		t.Fatal("request nao chegou ao servidor")
	}
	if capturado.Method != http.MethodPost {
		t.Fatalf("method %s", capturado.Method)
	}
	if capturado.URL.Path != "/v2/externo/cobrancas" {
		t.Fatalf("path %s", capturado.URL.Path)
	}
	if capturado.Header.Get("Idempotency-Key") != "idem-1" {
		t.Fatalf("idempotency %s", capturado.Header.Get("Idempotency-Key"))
	}
	if capturado.Header.Get("chave_api") != "api_key_exemplo" {
		t.Fatal("chave_api ausente")
	}
	if capturado.Header.Get("X-Api-Key") != "api_key_exemplo" {
		t.Fatal("X-Api-Key ausente")
	}
	if capturado.Header.Get("X-Client-Id") != "" {
		t.Fatal("X-Client-Id nao deve ser enviado sem signing")
	}
	if capturado.Header.Get("X-Timestamp") != "" {
		t.Fatal("X-Timestamp nao deve ser enviado sem signing")
	}
	if capturado.Header.Get("X-Nonce") != "" {
		t.Fatal("X-Nonce nao deve ser enviado sem signing")
	}
	if capturado.Header.Get("X-Signature") != "" {
		t.Fatal("X-Signature nao deve ser enviado sem signing")
	}
}

func TestCreateCobrancaComSigningEnabled(t *testing.T) {
	var capturado *http.Request
	var corpo []byte

	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		capturado = r.Clone(r.Context())
		corpo, _ = io.ReadAll(r.Body)
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusCreated)
		_ = json.NewEncoder(w).Encode(map[string]any{
			"data": map[string]any{
				"id":             "11111111-1111-4111-8111-111111111111",
				"valor_centavos": 15000,
				"vencimento":     "2026-10-01",
				"status":         "pendente",
				"pagador":        map[string]any{"documento": "1", "nome": "A"},
			},
		})
	}))
	defer srv.Close()

	client, err := NewClient(Options{
		ClientID:       "client_exemplo",
		APIKey:         "api_key_exemplo",
		ClientSecret:   "test_client_secret",
		SigningEnabled: true,
		BaseURL:        srv.URL + "/v1",
		Clock:          func() time.Time { return time.Unix(1700000000, 0) },
		NonceGenerator: func() string { return "550e8400-e29b-41d4-a716-446655440000" },
	})
	if err != nil {
		t.Fatal(err)
	}

	_, err = client.CreateCobranca(context.Background(), CreateCobrancaInput{
		ExternalReference: "pedido-1",
		ValorCentavos:     15000,
		Vencimento:        "2026-10-01",
		Pagador:           Pagador{Documento: "12345678901", Nome: "Maria Silva"},
		IdempotencyKey:    "idem-1",
	})
	if err != nil {
		t.Fatalf("create: %v", err)
	}

	if capturado.Header.Get("chave_api") != "api_key_exemplo" {
		t.Fatal("chave_api ausente")
	}
	if capturado.Header.Get("X-Api-Key") != "api_key_exemplo" {
		t.Fatal("X-Api-Key ausente")
	}
	if capturado.Header.Get("X-Client-Id") != "client_exemplo" {
		t.Fatal("X-Client-Id ausente")
	}
	signer := NewSigner("test_client_secret")
	esperado := signer.SignatureFor("POST", "/v1/cobrancas", "1700000000", "550e8400-e29b-41d4-a716-446655440000", corpo)
	if capturado.Header.Get("X-Signature") != esperado {
		t.Fatalf("assinatura invalida\n%s\n%s", capturado.Header.Get("X-Signature"), esperado)
	}
	if strings.Contains(string(corpo), "test_client_secret") {
		t.Fatal("client_secret vazou no body")
	}
}

func TestFindListCancelELicenca(t *testing.T) {
	mux := http.NewServeMux()
	mux.HandleFunc("/v2/externo/cobrancas/", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		if r.Header.Get("chave_api") == "" || r.Header.Get("X-Api-Key") == "" {
			t.Errorf("headers de api_key ausentes")
		}
		if r.Header.Get("X-Signature") != "" {
			t.Errorf("HMAC nao deveria estar presente")
		}
		switch {
		case strings.HasSuffix(r.URL.Path, "/cancel") && r.Method == http.MethodPost:
			_ = json.NewEncoder(w).Encode(map[string]any{
				"id":             "abc",
				"valor_centavos": 100,
				"vencimento":     "2026-10-01",
				"status":         "cancelada",
				"pagador":        map[string]any{"documento": "1", "nome": "A"},
			})
		case r.Method == http.MethodGet:
			_ = json.NewEncoder(w).Encode(map[string]any{
				"id":             "abc",
				"valor_centavos": 100,
				"vencimento":     "2026-10-01",
				"status":         "pendente",
				"pagador":        map[string]any{"documento": "1", "nome": "A"},
			})
		default:
			http.NotFound(w, r)
		}
	})
	mux.HandleFunc("/v2/externo/cobrancas", func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Query().Get("status") != "pendente" {
			t.Errorf("filtro status ausente")
		}
		if r.Header.Get("chave_api") == "" || r.Header.Get("X-Api-Key") == "" {
			t.Errorf("list sem headers de api_key")
		}
		_ = json.NewEncoder(w).Encode(map[string]any{
			"data": []map[string]any{{
				"id":             "abc",
				"valor_centavos": 100,
				"vencimento":     "2026-10-01",
				"status":         "pendente",
				"pagador":        map[string]any{"documento": "1", "nome": "A"},
			}},
			"meta": map[string]any{"page": 1, "per_page": 20, "total": 1},
		})
	})
	mux.HandleFunc("/v2/externo/licenses/verify", func(w http.ResponseWriter, r *http.Request) {
		_ = json.NewEncoder(w).Encode(map[string]any{
			"success": true,
			"data":    map[string]any{"valid": true, "message": "ok"},
		})
	})

	srv := httptest.NewServer(mux)
	defer srv.Close()

	client, err := NewClient(Options{
		APIKey:  "api_key_exemplo",
		BaseURL: srv.URL + "/v2/externo",
	})
	if err != nil {
		t.Fatal(err)
	}

	ctx := context.Background()
	found, err := client.FindCobranca(ctx, "abc")
	if err != nil || found.Status != "pendente" {
		t.Fatalf("find: %+v %v", found, err)
	}

	lista, err := client.ListCobrancas(ctx, ListCobrancasInput{Status: "pendente", Page: 1, PerPage: 20})
	if err != nil || len(lista.Data) != 1 {
		t.Fatalf("list: %+v %v", lista, err)
	}

	cancelada, err := client.CancelCobranca(ctx, "abc")
	if err != nil || cancelada.Status != "cancelada" {
		t.Fatalf("cancel: %+v %v", cancelada, err)
	}

	lic, err := client.VerifyLicense(ctx)
	if err != nil || !lic.Valid {
		t.Fatalf("license: %+v %v", lic, err)
	}
}

func TestNewClientRejeitaConfigIncompleta(t *testing.T) {
	_, err := NewClient(Options{})
	if err == nil {
		t.Fatal("esperava erro de configuracao")
	}
}

func TestNewClientAceitaSomenteAPIKey(t *testing.T) {
	client, err := NewClient(Options{APIKey: "chave"})
	if err != nil {
		t.Fatal(err)
	}
	if client == nil {
		t.Fatal("client nil")
	}
}

func TestNewClientSigningExigeSecret(t *testing.T) {
	_, err := NewClient(Options{APIKey: "chave", SigningEnabled: true})
	if err == nil {
		t.Fatal("esperava erro de ClientSecret")
	}
}

func TestFindNotFound(t *testing.T) {
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusNotFound)
		_, _ = w.Write([]byte(`{"message":"nao encontrada"}`))
	}))
	defer srv.Close()

	client, err := NewClient(Options{
		APIKey:  "k",
		BaseURL: srv.URL,
	})
	if err != nil {
		t.Fatal(err)
	}
	_, err = client.FindCobranca(context.Background(), "x")
	if _, ok := err.(*NotFoundError); !ok {
		t.Fatalf("esperava NotFoundError, obteve %T %v", err, err)
	}
}
