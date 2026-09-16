package agendacobranca

import (
	"testing"
	"time"
)

func signerDeterministico() *Signer {
	return NewSignerWith(
		"test_client_secret",
		func() time.Time { return time.Unix(1700000000, 0) },
		func() string { return "550e8400-e29b-41d4-a716-446655440000" },
	)
}

func TestCanonicalStringEAssinaturaCompartilhada(t *testing.T) {
	signer := signerDeterministico()
	body := []byte(`{"external_reference":"pedido-1","valor_centavos":15000}`)

	canonical := CanonicalString("POST", "/v1/cobrancas", "1700000000", "550e8400-e29b-41d4-a716-446655440000", body)
	esperadoCanonical := "POST\n/v1/cobrancas\n1700000000\n550e8400-e29b-41d4-a716-446655440000\nd453a65109b62169820d03d496180180c9ffd50248be209887b3786f374d0a75"
	if canonical != esperadoCanonical {
		t.Fatalf("canonical divergente\n%s\n%s", canonical, esperadoCanonical)
	}

	assinado := signer.Sign("POST", "/v1/cobrancas", body)
	if assinado.Signature != "f0334aadd5c24c375365a83cf301e8c39a5686f7680b0be51f84c95836cb60a3" {
		t.Fatalf("signature POST divergente: %s", assinado.Signature)
	}

	get := signer.Sign("GET", "/v1/cobrancas/abc", nil)
	if get.Signature != "19eadbfc80ca376521472ef1297e4513e5f8ef17dedcee5e88cc34a6492349c9" {
		t.Fatalf("signature GET divergente: %s", get.Signature)
	}
}

func TestVerifyWebhookVetorCompartilhado(t *testing.T) {
	ok := VerifyWebhook(
		[]byte(`{"id":"evt-1"}`),
		"1700000000",
		"550e8400-e29b-41d4-a716-446655440000",
		"bcf2bd3d052eaaa22c83a552dc1cdbf33e9613fa34196f6920dcfa02db0052f6",
		"test_client_secret",
	)
	if !ok {
		t.Fatal("esperava webhook valido")
	}
	if VerifyWebhook([]byte(`{"id":"evt-2"}`), "1700000000", "550e8400-e29b-41d4-a716-446655440000", "bcf2bd3d052eaaa22c83a552dc1cdbf33e9613fa34196f6920dcfa02db0052f6", "test_client_secret") {
		t.Fatal("esperava webhook invalido")
	}
}
