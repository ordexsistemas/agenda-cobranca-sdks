package agendacobranca

import (
	"net/http"
	"strings"
	"time"
)

const DefaultBaseURL = "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo"

type Options struct {
	APIKey              string
	BaseURL             string
	ClientID            string
	ClientSecret        string
	SigningEnabled      bool
	HTTPClient          *http.Client
	Timeout             time.Duration
	VerifyLicenseOnInit bool
	Clock               Clock
	NonceGenerator      NonceGenerator
}

func (o Options) validar() error {
	var faltando []string
	if strings.TrimSpace(o.APIKey) == "" {
		faltando = append(faltando, "APIKey")
	}
	if o.SigningEnabled && strings.TrimSpace(o.ClientSecret) == "" {
		faltando = append(faltando, "ClientSecret")
	}
	if len(faltando) > 0 {
		return &ConfigurationError{Message: "configuracao incompleta: " + strings.Join(faltando, ", ")}
	}
	return nil
}

func (o Options) baseURL() string {
	if strings.TrimSpace(o.BaseURL) == "" {
		return DefaultBaseURL
	}
	return strings.TrimRight(o.BaseURL, "/")
}

func (o Options) timeout() time.Duration {
	if o.Timeout <= 0 {
		return 30 * time.Second
	}
	return o.Timeout
}
