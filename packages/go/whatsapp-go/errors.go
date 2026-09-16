package whatsapp

import "fmt"

type APIError struct {
	Status  int
	Message string
	Body    []byte
}

func (e *APIError) Error() string {
	if e.Message != "" {
		return fmt.Sprintf("whatsapp sdk: HTTP %d: %s", e.Status, e.Message)
	}
	return fmt.Sprintf("whatsapp sdk: HTTP %d", e.Status)
}

func mapearErro(status int, message string, body []byte) error {
	err := &APIError{Status: status, Message: message, Body: body}
	switch status {
	case 401, 403:
		return &AuthenticationError{APIError: err}
	case 404:
		return &NotFoundError{APIError: err}
	case 422:
		return &ValidationError{APIError: err}
	case 429:
		return &RateLimitError{APIError: err}
	default:
		return err
	}
}

type AuthenticationError struct{ *APIError }
type NotFoundError struct{ *APIError }
type ValidationError struct{ *APIError }
type RateLimitError struct{ *APIError }

type ConfigurationError struct{ Message string }

func (e *ConfigurationError) Error() string { return e.Message }

type SignatureError struct{ Message string }

func (e *SignatureError) Error() string { return e.Message }

type EntitlementError struct {
	Message  string
	TenantID string
}

func (e *EntitlementError) Error() string { return e.Message }

type QuotaExceededError struct {
	Message  string
	Category MessageCategory
	Used     int64
	Quota    int64
	Period   string
}

func (e *QuotaExceededError) Error() string { return e.Message }
