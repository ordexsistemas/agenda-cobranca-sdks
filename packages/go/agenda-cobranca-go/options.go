package agendacobranca

import (
	"net/http"
	"strings"
	"time"
)

const DefaultBaseURL = "https://api.agendacobranca.example/v1"

type Options struct {
	ClientID                 string
	APIKey                   string
	ClientSecret             string
	BaseURL                  string
	HTTPClient               *http.Client
	Timeout                  time.Duration
	VerifyLicenseOnInit      bool
	Clock                    Clock
	NonceGenerator           NonceGenerator
}

func (o Options) validar() error {
	var faltando []string
	if strings.TrimSpace(o.ClientID) == "" {
		faltando = append(faltando, "ClientID")
	}
	if strings.TrimSpace(o.APIKey) == "" {
		faltando = append(faltando, "APIKey")
	}
	if strings.TrimSpace(o.ClientSecret) == "" {
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
