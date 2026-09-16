package agendacobranca

import (
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"strconv"
)

func (c *Client) CreateCobranca(ctx context.Context, in CreateCobrancaInput) (*Cobranca, error) {
	if in.ValorCentavos <= 0 {
		return nil, &ValidationError{APIError: &APIError{Status: http.StatusBadRequest, Message: "valor_centavos e obrigatorio"}}
	}
	if in.Vencimento == "" {
		return nil, &ValidationError{APIError: &APIError{Status: http.StatusBadRequest, Message: "vencimento e obrigatorio"}}
	}

	idem := in.IdempotencyKey
	if idem == "" {
		idem = gerarNonceUUIDv4()
	}

	payload := createCobrancaPayload{
		ExternalReference: in.ExternalReference,
		ValorCentavos:     in.ValorCentavos,
		Vencimento:        in.Vencimento,
		Pagador:           in.Pagador,
		Juros:             in.Juros,
		Multa:             in.Multa,
	}

	raw, _, err := c.do(ctx, http.MethodPost, "cobrancas", nil, payload, map[string]string{
		"Idempotency-Key": idem,
	})
	if err != nil {
		return nil, err
	}
	return parsearCobranca(raw)
}

func (c *Client) FindCobranca(ctx context.Context, id string) (*Cobranca, error) {
	if id == "" {
		return nil, &ValidationError{APIError: &APIError{Status: http.StatusBadRequest, Message: "id e obrigatorio"}}
	}
	raw, _, err := c.do(ctx, http.MethodGet, "cobrancas/"+id, nil, nil, nil)
	if err != nil {
		return nil, err
	}
	return parsearCobranca(raw)
}

func (c *Client) ListCobrancas(ctx context.Context, in ListCobrancasInput) (*ListCobrancasResult, error) {
	query := map[string]string{}
	if in.Status != "" {
		query["status"] = in.Status
	}
	if in.ExternalReference != "" {
		query["external_reference"] = in.ExternalReference
	}
	if in.Page > 0 {
		query["page"] = strconv.Itoa(in.Page)
	}
	if in.PerPage > 0 {
		query["per_page"] = strconv.Itoa(in.PerPage)
	}

	raw, _, err := c.do(ctx, http.MethodGet, "cobrancas", query, nil, nil)
	if err != nil {
		return nil, err
	}

	var env envelopeLista
	if err := decodificarEnvelope(raw, &env); err != nil {
		return nil, err
	}
	if env.Data == nil {
		var direto []Cobranca
		if err := json.Unmarshal(raw, &direto); err == nil {
			env.Data = direto
		}
	}
	meta := env.Meta
	if meta.Page == 0 {
		meta.Page = env.Page
	}
	if meta.PerPage == 0 {
		meta.PerPage = env.PerPage
	}
	if meta.Total == 0 {
		meta.Total = env.Total
	}
	return &ListCobrancasResult{Data: env.Data, Meta: meta}, nil
}

func (c *Client) CancelCobranca(ctx context.Context, id string) (*Cobranca, error) {
	if id == "" {
		return nil, &ValidationError{APIError: &APIError{Status: http.StatusBadRequest, Message: "id e obrigatorio"}}
	}
	raw, _, err := c.do(ctx, http.MethodPost, "cobrancas/"+id+"/cancel", nil, nil, nil)
	if err != nil {
		return nil, err
	}
	return parsearCobranca(raw)
}

func (c *Client) VerifyLicense(ctx context.Context) (*LicenseVerifyResult, error) {
	raw, _, err := c.do(ctx, http.MethodPost, "licenses/verify", nil, map[string]string{
		"client_id": c.clientID,
	}, nil)
	if err != nil {
		return nil, err
	}

	var env envelopeLicenca
	if err := decodificarEnvelope(raw, &env); err != nil {
		return nil, err
	}
	resultado := env.Data
	if env.Valid != nil {
		resultado.Valid = *env.Valid
	}
	if resultado.Message == "" {
		resultado.Message = env.Message
	}
	if !resultado.Valid && env.Success {
		resultado.Valid = true
	}
	return &resultado, nil
}

func parsearCobranca(raw []byte) (*Cobranca, error) {
	var env envelopeCobranca
	if err := json.Unmarshal(raw, &env); err != nil {
		return nil, fmt.Errorf("falha ao decodificar cobranca: %w", err)
	}
	if env.Data.ID != "" {
		return &env.Data, nil
	}
	var direto Cobranca
	if err := json.Unmarshal(raw, &direto); err != nil {
		return nil, err
	}
	if direto.ID == "" {
		return nil, fmt.Errorf("resposta de cobranca sem id")
	}
	return &direto, nil
}
