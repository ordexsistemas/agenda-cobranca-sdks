package whatsapp

import (
	"crypto/hmac"
	"crypto/sha256"
	"crypto/subtle"
	"encoding/hex"
	"encoding/json"
	"fmt"
)

type InboundMessage struct {
	From         string
	ID           string
	Timestamp    string
	Type         string
	Text         string
	CallbackData string
	Raw          map[string]any
}

func VerifyWebhookChallenge(query map[string]string, verifyToken string) (string, error) {
	if verifyToken == "" {
		return "", &ConfigurationError{Message: "WHATSAPP_VERIFY_TOKEN e obrigatorio"}
	}
	mode := first(query, "hub.mode", "hub_mode")
	token := first(query, "hub.verify_token", "hub_verify_token")
	challenge := first(query, "hub.challenge", "hub_challenge")
	if mode == "subscribe" && token == verifyToken {
		return challenge, nil
	}
	return "", &ValidationError{APIError: &APIError{Status: 403, Message: "Token de verificacao de webhook invalido"}}
}

func MetaSignatureHeader(rawBody, appSecret string) string {
	mac := hmac.New(sha256.New, []byte(appSecret))
	_, _ = mac.Write([]byte(rawBody))
	return "sha256=" + hex.EncodeToString(mac.Sum(nil))
}

func VerifyMetaSignature(rawBody, signatureHeader, appSecret string) (bool, error) {
	if appSecret == "" {
		return false, &ConfigurationError{Message: "WHATSAPP_APP_SECRET e obrigatorio"}
	}
	if signatureHeader == "" {
		return false, nil
	}
	expected := MetaSignatureHeader(rawBody, appSecret)
	if len(expected) != len(signatureHeader) {
		return false, nil
	}
	if subtle.ConstantTimeCompare([]byte(expected), []byte(signatureHeader)) == 1 {
		return true, nil
	}
	return false, nil
}

func VerifyMetaSignatureOrThrow(rawBody, signatureHeader, appSecret string) error {
	ok, err := VerifyMetaSignature(rawBody, signatureHeader, appSecret)
	if err != nil {
		return err
	}
	if !ok {
		return &SignatureError{Message: "Assinatura X-Hub-Signature-256 invalida"}
	}
	return nil
}

func ParseInboundMessages(payload any) []InboundMessage {
	var out []InboundMessage
	root := asRecord(payload)
	entries, _ := root["entry"].([]any)
	for _, entry := range entries {
		er := asRecord(entry)
		changes, _ := er["changes"].([]any)
		for _, change := range changes {
			cr := asRecord(change)
			value := asRecord(cr["value"])
			messages, _ := value["messages"].([]any)
			for _, msg := range messages {
				rec := asRecord(msg)
				text := ""
				if t, ok := rec["text"].(map[string]any); ok {
					text, _ = t["body"].(string)
				}
				cb, _ := rec["biz_opaque_callback_data"].(string)
				out = append(out, InboundMessage{
					From:         fmt.Sprint(zero(rec["from"])),
					ID:           fmt.Sprint(zero(rec["id"])),
					Timestamp:    fmt.Sprint(zero(rec["timestamp"])),
					Type:         fmt.Sprint(zero(rec["type"])),
					Text:         text,
					CallbackData: cb,
					Raw:          rec,
				})
			}
		}
	}
	return out
}

func first(m map[string]string, keys ...string) string {
	for _, k := range keys {
		if v, ok := m[k]; ok {
			return v
		}
	}
	return ""
}

func zero(v any) any {
	if v == nil {
		return ""
	}
	return v
}

// Ensure json is referenced if payload arrives as []byte.
func parseJSON(raw []byte) any {
	var payload any
	if err := json.Unmarshal(raw, &payload); err != nil {
		return map[string]any{"raw": string(raw)}
	}
	return payload
}
