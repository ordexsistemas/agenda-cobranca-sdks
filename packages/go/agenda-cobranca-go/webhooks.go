package agendacobranca

const DefaultWebhookPath = "/v1/webhooks"

func VerifyWebhook(payload []byte, timestamp, nonce, signature, clientSecret string) bool {
	return VerifyWebhookAt(payload, timestamp, nonce, signature, clientSecret, httpMethodPost, DefaultWebhookPath)
}

func VerifyWebhookAt(payload []byte, timestamp, nonce, signature, clientSecret, method, path string) bool {
	if clientSecret == "" || timestamp == "" || nonce == "" || signature == "" {
		return false
	}
	signer := NewSigner(clientSecret)
	return signer.ValidSignature(method, path, timestamp, nonce, signature, payload)
}

const httpMethodPost = "POST"
