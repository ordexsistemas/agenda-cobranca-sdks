package agendacobranca

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"strings"
)

type Client struct {
	baseURL    string
	httpClient *http.Client
	signer     *Signer
	clientID   string
	apiKey     string
}

func NewClient(opts Options) (*Client, error) {
	if err := opts.validar(); err != nil {
		return nil, err
	}

	signer := NewSignerWith(opts.ClientSecret, opts.Clock, opts.NonceGenerator)

	client := &Client{
		baseURL:    opts.baseURL(),
		httpClient: montarHTTPClient(opts, signer),
		signer:     signer,
		clientID:   opts.ClientID,
		apiKey:     opts.APIKey,
	}

	if opts.VerifyLicenseOnInit {
		if _, err := client.VerifyLicense(context.Background()); err != nil {
			return nil, err
		}
	}

	return client, nil
}

func montarHTTPClient(opts Options, signer *Signer) *http.Client {
	next := http.DefaultTransport
	timeout := opts.timeout()
	if opts.HTTPClient != nil {
		if opts.HTTPClient.Transport != nil {
			next = opts.HTTPClient.Transport
		}
		if opts.HTTPClient.Timeout > 0 {
			timeout = opts.HTTPClient.Timeout
		}
	}
	return &http.Client{
		Timeout: timeout,
		Transport: &signingRoundTripper{
			next:     next,
			signer:   signer,
			clientID: opts.ClientID,
			apiKey:   opts.APIKey,
		},
	}
}

func (c *Client) do(ctx context.Context, method, path string, query map[string]string, body any, headers map[string]string) ([]byte, int, error) {
	var reader io.Reader
	if body != nil {
		raw, err := json.Marshal(body)
		if err != nil {
			return nil, 0, fmt.Errorf("falha ao serializar payload: %w", err)
		}
		reader = bytes.NewReader(raw)
	}

	url := c.baseURL + "/" + strings.TrimLeft(path, "/")
	req, err := http.NewRequestWithContext(ctx, method, url, reader)
	if err != nil {
		return nil, 0, err
	}
	if body != nil {
		req.Header.Set("Content-Type", "application/json")
	}
	req.Header.Set("Accept", "application/json")
	for chave, valor := range headers {
		req.Header.Set(chave, valor)
	}
	if len(query) > 0 {
		q := req.URL.Query()
		for chave, valor := range query {
			if valor != "" {
				q.Set(chave, valor)
			}
		}
		req.URL.RawQuery = q.Encode()
	}

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return nil, 0, err
	}
	defer resp.Body.Close()

	raw, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, resp.StatusCode, err
	}
	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		return raw, resp.StatusCode, erroDaResposta(resp.StatusCode, raw)
	}
	return raw, resp.StatusCode, nil
}

func erroDaResposta(status int, raw []byte) error {
	var env envelopeErro
	mensagem := ""
	if err := json.Unmarshal(raw, &env); err == nil {
		mensagem = env.Message
		if mensagem == "" {
			mensagem = env.Error
		}
	}
	if mensagem == "" {
		mensagem = strings.TrimSpace(string(raw))
	}
	return mapearErro(status, mensagem, raw)
}

func decodificarEnvelope[T any](raw []byte, destino *T) error {
	if len(bytes.TrimSpace(raw)) == 0 {
		return fmt.Errorf("resposta vazia")
	}
	return json.Unmarshal(raw, destino)
}
