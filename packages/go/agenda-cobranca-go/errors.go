package agendacobranca

import (
	"fmt"
	"net/http"
)

type APIError struct {
	Status  int
	Message string
	Body    []byte
}

func (e *APIError) Error() string {
	if e.Message != "" {
		return fmt.Sprintf("agenda cobranca: HTTP %d: %s", e.Status, e.Message)
	}
	return fmt.Sprintf("agenda cobranca: HTTP %d", e.Status)
}

func mapearErro(status int, message string, body []byte) error {
	err := &APIError{Status: status, Message: message, Body: body}
	switch status {
	case http.StatusUnauthorized, http.StatusForbidden:
		return &AuthenticationError{APIError: err}
	case http.StatusNotFound:
		return &NotFoundError{APIError: err}
	case http.StatusUnprocessableEntity:
		return &ValidationError{APIError: err}
	default:
		return err
	}
}

type AuthenticationError struct{ *APIError }
type NotFoundError struct{ *APIError }
type ValidationError struct{ *APIError }

type ConfigurationError struct {
	Message string
}

func (e *ConfigurationError) Error() string {
	return e.Message
}
