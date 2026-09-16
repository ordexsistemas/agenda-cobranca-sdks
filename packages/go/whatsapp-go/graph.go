package whatsapp

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"strings"
	"time"
)

type HTTPDoer interface {
	Do(*http.Request) (*http.Response, error)
}

func defaultDoer(timeout time.Duration) HTTPDoer {
	if timeout <= 0 {
		timeout = 30 * time.Second
	}
	return &http.Client{Timeout: timeout}
}

type GraphClient struct {
	base          string
	accessToken   string
	phoneNumberID string
	wabaID        string
	http          HTTPDoer
}

func NewGraphClient(opts Options) *GraphClient {
	version := strings.TrimSpace(opts.GraphVersion)
	if version == "" {
		version = DefaultGraphVersion
	}
	root := strings.TrimRight(strings.TrimSpace(opts.GraphBaseURL), "/")
	if root == "" {
		root = DefaultGraphBaseURL
	}
	doer := opts.HTTPClient
	if doer == nil {
		doer = defaultDoer(opts.Timeout)
	}
	return &GraphClient{
		base:          root + "/" + version + "/",
		accessToken:   opts.AccessToken,
		phoneNumberID: opts.PhoneNumberID,
		wabaID:        opts.WabaID,
		http:          doer,
	}
}

func (g *GraphClient) SendMessage(in SendMessageInput) (MetaMessageResult, error) {
	body, err := BuildMessageBody(in)
	if err != nil {
		return MetaMessageResult{}, err
	}
	payload, err := g.request(http.MethodPost, g.phoneNumberID+"/messages", body)
	if err != nil {
		return MetaMessageResult{}, err
	}
	return parseMessageResult(payload), nil
}

func BuildMessageBody(in SendMessageInput) (map[string]any, error) {
	msgType := in.Type
	if msgType == "template" {
		msgType = "template"
	}
	body := map[string]any{
		"messaging_product": "whatsapp",
		"recipient_type":    "individual",
		"to":                in.To,
		"type":              msgType,
	}
	if cb := buildCallbackData(in); cb != "" {
		body["biz_opaque_callback_data"] = cb
	}
	if in.Type == "template" {
		if in.Template == nil || strings.TrimSpace(in.Template.Name) == "" {
			return nil, fmt.Errorf("template.name e obrigatorio para envio de template")
		}
		lang := in.Template.Language
		if lang == "" {
			lang = "pt_BR"
		}
		tpl := map[string]any{
			"name":     in.Template.Name,
			"language": map[string]any{"code": lang},
		}
		if in.Template.Components != nil {
			tpl["components"] = in.Template.Components
		}
		body["template"] = tpl
		return body, nil
	}
	if in.Type == "text" {
		if strings.TrimSpace(in.TextBody) == "" {
			return nil, fmt.Errorf("text.body e obrigatorio para mensagem de sessao")
		}
		body["text"] = map[string]any{"preview_url": in.PreviewURL, "body": in.TextBody}
		return body, nil
	}
	if in.Media == nil || (in.Media.ID == "" && in.Media.Link == "") {
		return nil, fmt.Errorf("media.id ou media.link e obrigatorio")
	}
	media := map[string]any{}
	if in.Media.ID != "" {
		media["id"] = in.Media.ID
	}
	if in.Media.Link != "" {
		media["link"] = in.Media.Link
	}
	if in.Media.Caption != "" {
		media["caption"] = in.Media.Caption
	}
	if in.Media.Filename != "" {
		media["filename"] = in.Media.Filename
	}
	body[in.Type] = media
	return body, nil
}

func buildCallbackData(in SendMessageInput) string {
	if in.CallbackData != "" {
		if len(in.CallbackData) > 512 {
			return in.CallbackData[:512]
		}
		return in.CallbackData
	}
	raw, _ := json.Marshal(map[string]string{"category": string(in.Category)})
	if len(raw) <= 512 {
		return string(raw)
	}
	return ""
}

func parseMessageResult(payload any) MetaMessageResult {
	rec, _ := payload.(map[string]any)
	out := MetaMessageResult{Raw: payload}
	if rec == nil {
		return out
	}
	if v, ok := rec["messaging_product"].(string); ok {
		out.MessagingProduct = v
	}
	if arr, ok := rec["contacts"].([]any); ok {
		for _, c := range arr {
			row, _ := c.(map[string]any)
			item := struct {
				Input string
				WaID  string
			}{}
			if row != nil {
				item.Input, _ = row["input"].(string)
				item.WaID, _ = row["wa_id"].(string)
			}
			out.Contacts = append(out.Contacts, item)
		}
	}
	if arr, ok := rec["messages"].([]any); ok {
		for _, m := range arr {
			row, _ := m.(map[string]any)
			item := struct {
				ID            string
				MessageStatus string
			}{}
			if row != nil {
				item.ID, _ = row["id"].(string)
				item.MessageStatus, _ = row["message_status"].(string)
			}
			out.Messages = append(out.Messages, item)
		}
	}
	return out
}

func (g *GraphClient) request(method, path string, body any) (any, error) {
	var reader io.Reader
	if body != nil {
		raw, err := json.Marshal(body)
		if err != nil {
			return nil, err
		}
		reader = bytes.NewReader(raw)
	}
	req, err := http.NewRequest(method, g.base+strings.TrimLeft(path, "/"), reader)
	if err != nil {
		return nil, err
	}
	req.Header.Set("Authorization", "Bearer "+g.accessToken)
	req.Header.Set("Accept", "application/json")
	if body != nil {
		req.Header.Set("Content-Type", "application/json")
	}
	resp, err := g.http.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()
	raw, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, err
	}
	payload := decodeBody(raw)
	if resp.StatusCode >= 200 && resp.StatusCode < 300 {
		return payload, nil
	}
	rec, _ := payload.(map[string]any)
	msg := fmt.Sprintf("Erro HTTP %d na Graph API", resp.StatusCode)
	if rec != nil {
		if errObj, ok := rec["error"].(map[string]any); ok {
			if m, ok := errObj["message"].(string); ok && m != "" {
				msg = m
			}
		} else if m, ok := rec["message"].(string); ok && m != "" {
			msg = m
		}
	}
	return nil, mapearErro(resp.StatusCode, msg, raw)
}

func decodeBody(raw []byte) any {
	trimmed := bytes.TrimSpace(raw)
	if len(trimmed) == 0 {
		return map[string]any{}
	}
	var payload any
	if err := json.Unmarshal(trimmed, &payload); err != nil {
		return map[string]any{"raw": string(raw)}
	}
	return payload
}

func asRecord(v any) map[string]any {
	if m, ok := v.(map[string]any); ok {
		return m
	}
	return map[string]any{}
}

func unwrapData(payload any) map[string]any {
	rec := asRecord(payload)
	if data, ok := rec["data"].(map[string]any); ok {
		return data
	}
	return rec
}

func isTruthy(v any) bool {
	switch t := v.(type) {
	case bool:
		return t
	case string:
		return t == "true" || t == "1"
	case float64:
		return t == 1
	case json.Number:
		n, _ := t.Int64()
		return n == 1
	default:
		return false
	}
}
