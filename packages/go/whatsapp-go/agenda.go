package whatsapp

import (
	"context"
	"strings"

	agendacobranca "agendacobranca.dev/sdk/go"
)

// AgendaSDKAdapter wraps the Agenda Cobrança Go client. HMAC/signing stay in that SDK.
type AgendaSDKAdapter struct {
	Client *agendacobranca.Client
}

func NewAgendaSDKAdapter(client *agendacobranca.Client) *AgendaSDKAdapter {
	return &AgendaSDKAdapter{Client: client}
}

func NewAgendaFromAPIKey(apiKey, baseURL string) (*AgendaSDKAdapter, error) {
	client, err := agendacobranca.NewClient(agendacobranca.Options{
		APIKey:  apiKey,
		BaseURL: baseURL,
	})
	if err != nil {
		return nil, err
	}
	return &AgendaSDKAdapter{Client: client}, nil
}

func (a *AgendaSDKAdapter) CreateCobranca(in CreateCobrancaInput) (CobrancaRecord, error) {
	created, err := a.Client.CreateCobranca(context.Background(), agendacobranca.CreateCobrancaInput{
		ExternalReference: in.ExternalReference,
		ValorCentavos:     in.ValorCentavos,
		Vencimento:        in.Vencimento,
		Pagador: agendacobranca.Pagador{
			Documento: in.Pagador.Documento,
			Nome:      in.Pagador.Nome,
			Email:     in.Pagador.Email,
		},
		IdempotencyKey: in.IdempotencyKey,
	})
	if err != nil {
		return CobrancaRecord{}, err
	}
	return mapAgendaCobranca(created), nil
}

func (a *AgendaSDKAdapter) ListCobrancas(in ListCobrancasInput) ([]CobrancaRecord, error) {
	listed, err := a.Client.ListCobrancas(context.Background(), agendacobranca.ListCobrancasInput{
		ExternalReference: in.ExternalReference,
		PerPage:           in.PerPage,
	})
	if err != nil {
		return nil, err
	}
	out := make([]CobrancaRecord, 0, len(listed.Data))
	for i := range listed.Data {
		out = append(out, mapAgendaCobranca(&listed.Data[i]))
	}
	return out, nil
}

func (a *AgendaSDKAdapter) CancelCobranca(id string) (CobrancaRecord, error) {
	cancelled, err := a.Client.CancelCobranca(context.Background(), id)
	if err != nil {
		return CobrancaRecord{}, err
	}
	return mapAgendaCobranca(cancelled), nil
}

func mapAgendaCobranca(c *agendacobranca.Cobranca) CobrancaRecord {
	if c == nil {
		return CobrancaRecord{}
	}
	p := Pagador{Documento: c.Pagador.Documento, Nome: c.Pagador.Nome, Email: c.Pagador.Email}
	return CobrancaRecord{
		ID:                c.ID,
		ExternalReference: c.ExternalReference,
		ValorCentavos:     c.ValorCentavos,
		Vencimento:        c.Vencimento,
		Status:            c.Status,
		Pagador:           &p,
	}
}

type CobrancaQuantityController struct {
	agenda        AgendaLike
	pagador       Pagador
	vencimentoDay int
}

func NewCobrancaQuantityController(agenda AgendaLike, pagador Pagador, vencimentoDay int) *CobrancaQuantityController {
	return &CobrancaQuantityController{agenda: agenda, pagador: pagador, vencimentoDay: vencimentoDay}
}

func (c *CobrancaQuantityController) Sync(plan CobrancaQuantityPlan) (CobrancaSyncResult, error) {
	var items []CobrancaSyncItem
	for _, categoryPlan := range plan.Categories {
		vencimento, err := VencimentoForPeriod(plan.Period, c.vencimentoDay)
		if err != nil {
			return CobrancaSyncResult{}, err
		}
		for index := 1; index <= categoryPlan.Quantity; index++ {
			ref := CobrancaExternalReference(plan.TenantID, plan.Period, categoryPlan.Category, index)
			existing, err := c.findByRef(ref)
			if err != nil {
				return CobrancaSyncResult{}, err
			}
			if existing != nil && !isCancelled(*existing) {
				items = append(items, CobrancaSyncItem{Category: categoryPlan.Category, Index: index, ExternalReference: ref, Action: "kept", Cobranca: existing})
				continue
			}
			valor := int64(0)
			if index-1 < len(categoryPlan.ValorCentavosEach) {
				valor = categoryPlan.ValorCentavosEach[index-1]
			}
			created, err := c.agenda.CreateCobranca(CreateCobrancaInput{
				ExternalReference: ref,
				ValorCentavos:     valor,
				Vencimento:        vencimento,
				Pagador:           c.pagador,
				IdempotencyKey:    ref,
			})
			if err != nil {
				return CobrancaSyncResult{}, err
			}
			cp := created
			items = append(items, CobrancaSyncItem{Category: categoryPlan.Category, Index: index, ExternalReference: ref, Action: "created", Cobranca: &cp})
		}
		extra, err := c.cancelExtras(plan, categoryPlan.Category, categoryPlan.Quantity)
		if err != nil {
			return CobrancaSyncResult{}, err
		}
		items = append(items, extra...)
	}
	return summarize(plan, items), nil
}

func (c *CobrancaQuantityController) findByRef(ref string) (*CobrancaRecord, error) {
	listed, err := c.agenda.ListCobrancas(ListCobrancasInput{ExternalReference: ref, PerPage: 20})
	if err != nil {
		return nil, err
	}
	for i := range listed {
		if listed[i].ExternalReference == ref && !isCancelled(listed[i]) {
			item := listed[i]
			return &item, nil
		}
	}
	return nil, nil
}

func (c *CobrancaQuantityController) cancelExtras(plan CobrancaQuantityPlan, category MessageCategory, keep int) ([]CobrancaSyncItem, error) {
	var items []CobrancaSyncItem
	scanUntil := keep + 20
	for index := keep + 1; index <= scanUntil; index++ {
		ref := CobrancaExternalReference(plan.TenantID, plan.Period, category, index)
		existing, err := c.findByRef(ref)
		if err != nil {
			return nil, err
		}
		if existing == nil {
			break
		}
		if _, err := c.agenda.CancelCobranca(existing.ID); err != nil {
			return nil, err
		}
		items = append(items, CobrancaSyncItem{Category: category, Index: index, ExternalReference: ref, Action: "cancelled", Cobranca: existing})
	}
	return items, nil
}

func isCancelled(c CobrancaRecord) bool {
	status := strings.ToLower(c.Status)
	return status == "cancelada" || status == "cancelled" || status == "canceled"
}

func summarize(plan CobrancaQuantityPlan, items []CobrancaSyncItem) CobrancaSyncResult {
	out := CobrancaSyncResult{Period: plan.Period, TenantID: plan.TenantID, Plan: plan, Items: items}
	for _, i := range items {
		switch i.Action {
		case "created":
			out.Created++
		case "cancelled":
			out.Cancelled++
		case "kept":
			out.Kept++
		}
	}
	return out
}
