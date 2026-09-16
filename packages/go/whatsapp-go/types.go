package whatsapp

import "time"

const (
	DefaultOrdexBaseURL  = "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo"
	DefaultGraphBaseURL  = "https://graph.facebook.com"
	DefaultGraphVersion  = "v21.0"
	DefaultUsdToBrl      = 5.5
	ServiceFreeAllowance = 1000
	DefaultSessionsPer   = int64(10_000)
	DefaultValorCentavos = int64(100_000)
)

type MessageCategory string

const (
	CategoryAuth      MessageCategory = "auth"
	CategoryUtility   MessageCategory = "utility"
	CategoryService   MessageCategory = "service"
	CategoryMarketing MessageCategory = "marketing"
)

var MessageCategories = []MessageCategory{
	CategoryAuth, CategoryUtility, CategoryService, CategoryMarketing,
}

type QuotaMode string

const (
	QuotaHard QuotaMode = "hard"
	QuotaSoft QuotaMode = "soft"
)

type QuantityStrategy string

const (
	StrategyPerCategory QuantityStrategy = "per-category"
	StrategyBySessions  QuantityStrategy = "by-sessions"
	StrategyByValue     QuantityStrategy = "by-value"
)

type CategoryQuota struct {
	MonthlyQuota  int64
	UnitCostUSD   float64
	FreeAllowance int64
}

type PlanQuotas struct {
	Categories map[MessageCategory]CategoryQuota
	UsdToBrl   float64
	QuotaMode  QuotaMode
}

type UsageCounts struct {
	Auth      int64 `json:"auth"`
	Utility   int64 `json:"utility"`
	Service   int64 `json:"service"`
	Marketing int64 `json:"marketing"`
}

func (c UsageCounts) Get(cat MessageCategory) int64 {
	switch cat {
	case CategoryAuth:
		return c.Auth
	case CategoryUtility:
		return c.Utility
	case CategoryService:
		return c.Service
	case CategoryMarketing:
		return c.Marketing
	default:
		return 0
	}
}

func (c *UsageCounts) Add(cat MessageCategory, delta int64) {
	switch cat {
	case CategoryAuth:
		c.Auth = max64(0, c.Auth+delta)
	case CategoryUtility:
		c.Utility = max64(0, c.Utility+delta)
	case CategoryService:
		c.Service = max64(0, c.Service+delta)
	case CategoryMarketing:
		c.Marketing = max64(0, c.Marketing+delta)
	}
}

func EmptyUsageCounts() UsageCounts { return UsageCounts{} }

type UsageSnapshot struct {
	TenantID string
	Period   string
	Counts   UsageCounts
}

type UsageStore interface {
	Get(tenantID, period string) (UsageSnapshot, error)
	Increment(tenantID, period string, category MessageCategory, delta int64) (UsageSnapshot, error)
}

type MeteringDecision struct {
	Category      MessageCategory
	Period        string
	Used          int64
	Quota         int64
	Remaining     int64
	BillableDelta int64
	FreeDelta     int64
	Overage       bool
	OverageDelta  int64
	Allowed       bool
	QuotaMode     QuotaMode
}

type Pagador struct {
	Documento string
	Nome      string
	Email     string
}

type CobrancaRecord struct {
	ID                string
	ExternalReference string
	ValorCentavos     int64
	Vencimento        string
	Status            string
	Pagador           *Pagador
}

type CreateCobrancaInput struct {
	ExternalReference string
	ValorCentavos     int64
	Vencimento        string
	Pagador           Pagador
	IdempotencyKey    string
}

type ListCobrancasInput struct {
	ExternalReference string
	PerPage           int
}

// AgendaLike is the surface used by the WhatsApp add-on.
// Implemented by AgendaSDKAdapter wrapping agendacobranca.dev/sdk/go.
type AgendaLike interface {
	CreateCobranca(in CreateCobrancaInput) (CobrancaRecord, error)
	ListCobrancas(in ListCobrancasInput) ([]CobrancaRecord, error)
	CancelCobranca(id string) (CobrancaRecord, error)
}

type TemplatePayload struct {
	Name       string
	Language   string
	Components []map[string]any
}

type MediaPayload struct {
	Type     string
	ID       string
	Link     string
	Caption  string
	Filename string
}

type SendMessageInput struct {
	To           string
	Category     MessageCategory
	Type         string
	Template     *TemplatePayload
	TextBody     string
	PreviewURL   bool
	Media        *MediaPayload
	CallbackData string
}

type MetaMessageResult struct {
	MessagingProduct string
	Contacts         []struct {
		Input string
		WaID  string
	}
	Messages []struct {
		ID            string
		MessageStatus string
	}
	Raw any
}

type SendMessageResult struct {
	Category     MessageCategory
	To           string
	Meta         MetaMessageResult
	Metering     MeteringDecision
	CobrancaPlan *CobrancaQuantityPlan
}

type CategoryCobrancaPlan struct {
	Category          MessageCategory
	Used              int64
	Billable          int64
	Free              int64
	Quota             int64
	Overage           int64
	UnitCostUSD       float64
	CostUSD           float64
	CostBrlCentavos   int64
	Quantity          int
	ValorCentavosEach []int64
}

type CobrancaQuantityPlan struct {
	Period               string
	TenantID             string
	Categories           []CategoryCobrancaPlan
	TotalQuantity        int
	TotalCostUSD         float64
	TotalCostBrlCentavos int64
	Strategy             QuantityStrategy
}

type ConversionOptions struct {
	Strategy                  QuantityStrategy
	SessionsPerCobranca       int64
	SessionsPerCobrancaByCat  map[MessageCategory]int64
	ValorCentavosPorCobranca  int64
	IncludeZeroCategories     bool
	Period                    string
	TenantID                  string
}

type CobrancaSyncItem struct {
	Category          MessageCategory
	Index             int
	ExternalReference string
	Action            string
	Cobranca          *CobrancaRecord
}

type CobrancaSyncResult struct {
	Period    string
	TenantID  string
	Plan      CobrancaQuantityPlan
	Items     []CobrancaSyncItem
	Created   int
	Cancelled int
	Kept      int
}

type EntitlementResult struct {
	Enabled  bool
	TenantID string
	AddOn    string
	Plan     string
	Message  string
	Raw      any
}

type EntitlementChecker interface {
	Check(tenantID string) (EntitlementResult, error)
}

type CategoryQuotaPatch struct {
	MonthlyQuota  *int64
	UnitCostUSD   *float64
	FreeAllowance *int64
}

type PlanOverride struct {
	UsdToBrl   *float64
	QuotaMode  QuotaMode
	Categories map[MessageCategory]CategoryQuotaPatch
}

type Options struct {
	AccessToken   string
	PhoneNumberID string
	WabaID        string
	GraphVersion  string
	GraphBaseURL  string
	AppSecret     string

	TenantID     string
	OrdexAPIKey  string
	OrdexBaseURL string

	Plan      *PlanOverride
	QuotaMode QuotaMode
	Store     UsageStore
	Clock     func() time.Time
	TimeZone  string

	Entitlement           EntitlementChecker
	SkipEntitlementCheck  bool
	Agenda                AgendaLike
	AgendaPagador         *Pagador
	AgendaVencimentoDay   int
	SyncCobrancasOnSend   bool
	QuantityStrategy      QuantityStrategy
	SessionsPerCobranca   int64
	ValorCentavosPorCobranca int64

	HTTPClient HTTPDoer
	Timeout    time.Duration
}

func max64(a, b int64) int64 {
	if a > b {
		return a
	}
	return b
}

func min64(a, b int64) int64 {
	if a < b {
		return a
	}
	return b
}
