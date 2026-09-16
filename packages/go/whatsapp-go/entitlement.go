package whatsapp

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"strings"
)

type StaticEntitlementChecker struct {
	Enabled bool
	Plan    string
	Message string
}

func (s StaticEntitlementChecker) Check(tenantID string) (EntitlementResult, error) {
	plan := s.Plan
	if plan == "" {
		plan = "saas"
	}
	return EntitlementResult{Enabled: s.Enabled, TenantID: tenantID, AddOn: "whatsapp", Plan: plan, Message: s.Message}, nil
}

type OrdexPayEntitlementChecker struct {
	apiKey                  string
	baseURL                 string
	http                    HTTPDoer
	clientID                string
	treatValidLicenseAsAddon bool
}

func NewOrdexPayEntitlementChecker(apiKey string, baseURL string, doer HTTPDoer) *OrdexPayEntitlementChecker {
	if strings.TrimSpace(baseURL) == "" {
		baseURL = DefaultOrdexBaseURL
	}
	if doer == nil {
		doer = defaultDoer(0)
	}
	return &OrdexPayEntitlementChecker{
		apiKey:  apiKey,
		baseURL: strings.TrimRight(baseURL, "/") + "/",
		http:    doer,
	}
}

func (c *OrdexPayEntitlementChecker) Check(tenantID string) (EntitlementResult, error) {
	payload, err := c.post("addons/whatsapp/entitlement", map[string]any{
		"tenant_id": tenantID,
		"client_id": c.clientID,
	})
	if err == nil {
		return fromPayload(tenantID, payload, "addons/whatsapp/entitlement"), nil
	}
	if _, ok := err.(*NotFoundError); !ok {
		return EntitlementResult{}, err
	}
	license, err := c.post("licenses/verify", map[string]any{
		"client_id": c.clientID,
		"tenant_id": tenantID,
	})
	if err != nil {
		return EntitlementResult{}, err
	}
	return c.fromLicense(tenantID, license), nil
}

func (c *OrdexPayEntitlementChecker) post(path string, body map[string]any) (any, error) {
	raw, _ := json.Marshal(body)
	req, err := http.NewRequest(http.MethodPost, c.baseURL+strings.TrimLeft(path, "/"), bytes.NewReader(raw))
	if err != nil {
		return nil, err
	}
	req.Header.Set("Accept", "application/json")
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("chave_api", c.apiKey)
	req.Header.Set("X-Api-Key", c.apiKey)
	resp, err := c.http.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()
	b, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, err
	}
	payload := decodeBody(b)
	if resp.StatusCode >= 200 && resp.StatusCode < 300 {
		return payload, nil
	}
	rec := asRecord(payload)
	msg := fmt.Sprintf("Erro HTTP %d", resp.StatusCode)
	if m, ok := rec["message"].(string); ok && m != "" {
		msg = m
	} else if m, ok := rec["error"].(string); ok && m != "" {
		msg = m
	}
	return nil, mapearErro(resp.StatusCode, msg, b)
}

func fromPayload(tenantID string, payload any, source string) EntitlementResult {
	rec := asRecord(payload)
	dados := unwrapData(payload)
	enabled := isTruthy(dados["enabled"]) || isTruthy(dados["valid"]) || isTruthy(rec["enabled"]) || isTruthy(rec["valid"]) || addonFlag(dados) || addonFlag(rec)
	msg, _ := dados["message"].(string)
	if msg == "" {
		msg, _ = rec["message"].(string)
	}
	if msg == "" {
		msg = source
	}
	plan, _ := dados["plan"].(string)
	if plan == "" {
		plan, _ = rec["plan"].(string)
	}
	if plan == "" {
		plan = "saas"
	}
	return EntitlementResult{Enabled: enabled, TenantID: tenantID, AddOn: "whatsapp", Plan: plan, Message: msg, Raw: payload}
}

func (c *OrdexPayEntitlementChecker) fromLicense(tenantID string, payload any) EntitlementResult {
	rec := asRecord(payload)
	dados := unwrapData(payload)
	licenseValid := isTruthy(dados["valid"]) || isTruthy(rec["valid"]) || isTruthy(rec["success"])
	addon := addonFlag(dados) || addonFlag(rec)
	enabled := addon || (c.treatValidLicenseAsAddon && licenseValid)
	msg := "licenses/verify sem add-on whatsapp habilitado"
	if enabled {
		msg = "add-on whatsapp autorizado via licenses/verify"
	}
	return EntitlementResult{Enabled: enabled, TenantID: tenantID, AddOn: "whatsapp", Plan: "saas", Message: msg, Raw: payload}
}

func addonFlag(rec map[string]any) bool {
	if isTruthy(rec["whatsapp_addon"]) || isTruthy(rec["addon_whatsapp"]) || isTruthy(rec["whatsapp"]) {
		return true
	}
	addons, ok := rec["addons"].(map[string]any)
	if !ok {
		return false
	}
	wa := addons["whatsapp"]
	if isTruthy(wa) {
		return true
	}
	if inner, ok := wa.(map[string]any); ok {
		return isTruthy(inner["enabled"]) || isTruthy(inner["valid"])
	}
	return false
}

func assertEntitlement(checker EntitlementChecker, tenantID string, skip bool) error {
	if skip {
		return nil
	}
	if checker == nil {
		return &EntitlementError{
			Message:  "EntitlementChecker ausente: injete StaticEntitlementChecker (demo) ou OrdexPayEntitlementChecker",
			TenantID: tenantID,
		}
	}
	result, err := checker.Check(tenantID)
	if err != nil {
		return err
	}
	if !result.Enabled {
		msg := result.Message
		if msg == "" {
			msg = "Add-on WhatsApp do plano SaaS Ordex Pay nao esta habilitado para este tenant"
		}
		return &EntitlementError{Message: msg, TenantID: tenantID}
	}
	return nil
}
