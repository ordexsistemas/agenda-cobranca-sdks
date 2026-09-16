package agendacobranca

import (
	"bytes"
	"crypto/hmac"
	"crypto/sha256"
	"encoding/hex"
	"io"
	"net/http"
	"strconv"
	"strings"
	"time"
)

type Clock func() time.Time

type NonceGenerator func() string

type Signer struct {
	clientSecret   string
	clock          Clock
	nonceGenerator NonceGenerator
}

func NewSigner(clientSecret string) *Signer {
	return NewSignerWith(clientSecret, time.Now, gerarNonceUUIDv4)
}

func NewSignerWith(clientSecret string, clock Clock, nonceGenerator NonceGenerator) *Signer {
	if clock == nil {
		clock = time.Now
	}
	if nonceGenerator == nil {
		nonceGenerator = gerarNonceUUIDv4
	}
	return &Signer{
		clientSecret:   clientSecret,
		clock:          clock,
		nonceGenerator: nonceGenerator,
	}
}

func HashCorpo(body []byte) string {
	if body == nil {
		body = []byte{}
	}
	soma := sha256.Sum256(body)
	return hex.EncodeToString(soma[:])
}

func CanonicalString(method, path, timestamp, nonce string, body []byte) string {
	return strings.ToUpper(method) + "\n" +
		path + "\n" +
		timestamp + "\n" +
		nonce + "\n" +
		HashCorpo(body)
}

func (s *Signer) SignatureFor(method, path, timestamp, nonce string, body []byte) string {
	canonical := CanonicalString(method, path, timestamp, nonce, body)
	mac := hmac.New(sha256.New, []byte(s.clientSecret))
	_, _ = mac.Write([]byte(canonical))
	return hex.EncodeToString(mac.Sum(nil))
}

type SignedHeaders struct {
	Timestamp string
	Nonce     string
	Signature string
	BodyHash  string
}

func (s *Signer) Sign(method, path string, body []byte) SignedHeaders {
	timestamp := strconv.FormatInt(s.clock().Unix(), 10)
	nonce := s.nonceGenerator()
	return SignedHeaders{
		Timestamp: timestamp,
		Nonce:     nonce,
		Signature: s.SignatureFor(method, path, timestamp, nonce, body),
		BodyHash:  HashCorpo(body),
	}
}

func (s *Signer) ValidSignature(method, path, timestamp, nonce, signature string, body []byte) bool {
	esperado := s.SignatureFor(method, path, timestamp, nonce, body)
	return hmac.Equal([]byte(esperado), []byte(signature))
}

type signingRoundTripper struct {
	next            http.RoundTripper
	signer          *Signer
	clientID        string
	apiKey          string
	signingEnabled  bool
}

func (t *signingRoundTripper) RoundTrip(req *http.Request) (*http.Response, error) {
	// Ordex Pay external API: always send both api-key headers with the same value.
	req.Header.Set("chave_api", t.apiKey)
	req.Header.Set("X-Api-Key", t.apiKey)

	if t.signingEnabled {
		corpo, err := lerBodyRequest(req)
		if err != nil {
			return nil, err
		}
		assinatura := t.signer.Sign(req.Method, req.URL.Path, corpo)
		req.Header.Set("X-Client-Id", t.clientID)
		req.Header.Set("X-Timestamp", assinatura.Timestamp)
		req.Header.Set("X-Nonce", assinatura.Nonce)
		req.Header.Set("X-Signature", assinatura.Signature)
	}

	return t.next.RoundTrip(req)
}

func lerBodyRequest(req *http.Request) ([]byte, error) {
	if req.Body == nil {
		return []byte{}, nil
	}
	corpo, err := io.ReadAll(req.Body)
	if err != nil {
		return nil, err
	}
	_ = req.Body.Close()
	req.Body = io.NopCloser(bytes.NewReader(corpo))
	req.GetBody = func() (io.ReadCloser, error) {
		return io.NopCloser(bytes.NewReader(corpo)), nil
	}
	req.ContentLength = int64(len(corpo))
	return corpo, nil
}
