package agendacobranca

type Pagador struct {
	Documento string `json:"documento"`
	Nome      string `json:"nome"`
	Email     string `json:"email,omitempty"`
}

type Juros struct {
	PercentualMes *float64 `json:"percentual_mes,omitempty"`
}

type Multa struct {
	Percentual *float64 `json:"percentual,omitempty"`
}

type Cobranca struct {
	ID                string  `json:"id"`
	ExternalReference string  `json:"external_reference,omitempty"`
	ValorCentavos     int64   `json:"valor_centavos"`
	Vencimento        string  `json:"vencimento"`
	Status            string  `json:"status"`
	Pagador           Pagador `json:"pagador"`
	Juros             *Juros  `json:"juros,omitempty"`
	Multa             *Multa  `json:"multa,omitempty"`
}

type CreateCobrancaInput struct {
	ExternalReference string
	ValorCentavos     int64
	Vencimento        string
	Pagador           Pagador
	Juros             *Juros
	Multa             *Multa
	IdempotencyKey    string
}

type createCobrancaPayload struct {
	ExternalReference string  `json:"external_reference,omitempty"`
	ValorCentavos     int64   `json:"valor_centavos"`
	Vencimento        string  `json:"vencimento"`
	Pagador           Pagador `json:"pagador"`
	Juros             *Juros  `json:"juros,omitempty"`
	Multa             *Multa  `json:"multa,omitempty"`
}

type ListCobrancasInput struct {
	Status            string
	ExternalReference string
	Page              int
	PerPage           int
}

type ListCobrancasResult struct {
	Data []Cobranca
	Meta ListMeta
}

type ListMeta struct {
	Page    int `json:"page,omitempty"`
	PerPage int `json:"per_page,omitempty"`
	Total   int `json:"total,omitempty"`
}

type LicenseVerifyResult struct {
	Valid   bool   `json:"valid"`
	Message string `json:"message,omitempty"`
}

type envelopeCobranca struct {
	Success bool      `json:"success"`
	Message string    `json:"message"`
	Data    Cobranca  `json:"data"`
	Errors  []string  `json:"errors"`
}

type envelopeLista struct {
	Success bool       `json:"success"`
	Message string     `json:"message"`
	Data    []Cobranca `json:"data"`
	Meta    ListMeta   `json:"meta"`
	Page    int        `json:"page"`
	PerPage int        `json:"per_page"`
	Total   int        `json:"total"`
}

type envelopeLicenca struct {
	Success bool                 `json:"success"`
	Message string               `json:"message"`
	Data    LicenseVerifyResult  `json:"data"`
	Valid   *bool                `json:"valid"`
}

type envelopeErro struct {
	Success bool     `json:"success"`
	Message string   `json:"message"`
	Error   string   `json:"error"`
	Errors  []string `json:"errors"`
}
