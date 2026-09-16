package whatsapp

import (
	"strings"
	"time"
)

type Client struct {
	opts             Options
	Metering         *MeteringService
	Graph            *GraphClient
	plan             PlanQuotas
	entitlement      EntitlementChecker
	agendaController *CobrancaQuantityController
	conversion       ConversionOptions
}

func NewClient(opts Options) (*Client, error) {
	var missing []string
	if strings.TrimSpace(opts.AccessToken) == "" {
		missing = append(missing, "AccessToken")
	}
	if strings.TrimSpace(opts.PhoneNumberID) == "" {
		missing = append(missing, "PhoneNumberID")
	}
	if strings.TrimSpace(opts.TenantID) == "" {
		missing = append(missing, "TenantID")
	}
	if len(missing) > 0 {
		return nil, &ConfigurationError{Message: "Configuracao incompleta: " + strings.Join(missing, ", ")}
	}

	plan := MergePlan(opts.Plan, opts.QuotaMode)
	store := opts.Store
	if store == nil {
		store = NewInMemoryUsageStore()
	}
	clock := opts.Clock
	if clock == nil {
		clock = time.Now
	}
	tz := opts.TimeZone
	if tz == "" {
		tz = "America/Sao_Paulo"
	}

	c := &Client{
		opts:     opts,
		plan:     plan,
		Metering: NewMeteringService(plan, store, clock, tz),
		Graph:    NewGraphClient(opts),
		conversion: ConversionOptions{
			Strategy:                 opts.QuantityStrategy,
			SessionsPerCobranca:      opts.SessionsPerCobranca,
			ValorCentavosPorCobranca: opts.ValorCentavosPorCobranca,
		},
	}
	c.entitlement = resolveEntitlement(opts)
	if agenda := resolveAgenda(opts); agenda != nil && opts.AgendaPagador != nil {
		c.agendaController = NewCobrancaQuantityController(agenda, *opts.AgendaPagador, opts.AgendaVencimentoDay)
	}
	return c, nil
}

func (c *Client) Plan() PlanQuotas { return c.plan }

func (c *Client) Usage(period string) (UsageSnapshot, error) {
	return c.Metering.Snapshot(c.opts.TenantID, period)
}

func (c *Client) CobrancaPlan(counts UsageCounts, period string) CobrancaQuantityPlan {
	if period == "" {
		period = c.Metering.Period()
	}
	opts := c.conversion
	opts.Period = period
	opts.TenantID = c.opts.TenantID
	return ComputeCobrancaQuantity(counts, c.plan, opts)
}

func (c *Client) PreviewCobrancaPlan(period string) (CobrancaQuantityPlan, error) {
	snap, err := c.Usage(period)
	if err != nil {
		return CobrancaQuantityPlan{}, err
	}
	return c.CobrancaPlan(snap.Counts, snap.Period), nil
}

func (c *Client) SyncCobrancas(period string) (CobrancaSyncResult, error) {
	if c.agendaController == nil {
		return CobrancaSyncResult{}, &ConfigurationError{Message: "Agenda de Cobrancas nao configurada: informe Agenda (ou OrdexAPIKey) e AgendaPagador"}
	}
	plan, err := c.PreviewCobrancaPlan(period)
	if err != nil {
		return CobrancaSyncResult{}, err
	}
	return c.agendaController.Sync(plan)
}

func (c *Client) SendTemplate(to string, category MessageCategory, template TemplatePayload, callbackData string) (SendMessageResult, error) {
	return c.Send(SendMessageInput{To: to, Category: category, Type: "template", Template: &template, CallbackData: callbackData})
}

func (c *Client) SendText(to, body string, category MessageCategory, callbackData string) (SendMessageResult, error) {
	if category == "" {
		category = CategoryService
	}
	return c.Send(SendMessageInput{To: to, Category: category, Type: "text", TextBody: body, CallbackData: callbackData})
}

func (c *Client) SendMedia(to string, category MessageCategory, media MediaPayload, callbackData string) (SendMessageResult, error) {
	return c.Send(SendMessageInput{To: to, Category: category, Type: media.Type, Media: &media, CallbackData: callbackData})
}

func (c *Client) Send(in SendMessageInput) (SendMessageResult, error) {
	if err := assertEntitlement(c.entitlement, c.opts.TenantID, c.opts.SkipEntitlementCheck); err != nil {
		return SendMessageResult{}, err
	}
	metering, err := c.Metering.Consume(c.opts.TenantID, in.Category, 1)
	if err != nil {
		return SendMessageResult{}, err
	}
	meta, err := c.Graph.SendMessage(in)
	if err != nil {
		_, _ = c.Metering.Rollback(c.opts.TenantID, in.Category, 1)
		return SendMessageResult{}, err
	}
	var plan CobrancaQuantityPlan
	if c.opts.SyncCobrancasOnSend && c.agendaController != nil {
		sync, syncErr := c.SyncCobrancas(metering.Period)
		if syncErr != nil {
			return SendMessageResult{}, syncErr
		}
		plan = sync.Plan
	} else {
		snap, snapErr := c.Usage(metering.Period)
		if snapErr != nil {
			return SendMessageResult{}, snapErr
		}
		plan = c.CobrancaPlan(snap.Counts, snap.Period)
	}
	return SendMessageResult{Category: in.Category, To: in.To, Meta: meta, Metering: metering, CobrancaPlan: &plan}, nil
}

func resolveEntitlement(opts Options) EntitlementChecker {
	if opts.Entitlement != nil {
		return opts.Entitlement
	}
	if opts.SkipEntitlementCheck {
		return StaticEntitlementChecker{Enabled: true, Plan: "demo"}
	}
	if strings.TrimSpace(opts.OrdexAPIKey) != "" {
		return NewOrdexPayEntitlementChecker(opts.OrdexAPIKey, opts.OrdexBaseURL, opts.HTTPClient)
	}
	return nil
}

func resolveAgenda(opts Options) AgendaLike {
	if opts.Agenda != nil {
		return opts.Agenda
	}
	if strings.TrimSpace(opts.OrdexAPIKey) != "" {
		adapter, err := NewAgendaFromAPIKey(opts.OrdexAPIKey, opts.OrdexBaseURL)
		if err != nil {
			return nil
		}
		return adapter
	}
	return nil
}
