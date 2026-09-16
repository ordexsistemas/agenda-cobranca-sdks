# frozen_string_literal: true

require "faraday"
require "json"

module OrdexWhatsApp
  class StaticEntitlementChecker
    def initialize(enabled, plan: "saas", message: nil)
      @enabled = enabled
      @plan = plan
      @message = message
    end

    def check(tenant_id)
      { enabled: @enabled, tenant_id: tenant_id, add_on: "whatsapp", plan: @plan, message: @message }
    end
  end

  class OrdexPayEntitlementChecker
    def initialize(api_key, base_url: nil, connection: nil, treat_valid_license_as_addon: false)
      @api_key = api_key
      @base_url = (base_url.nil? || base_url.to_s.strip.empty? ? DEFAULT_ORDEX_BASE_URL : base_url).to_s.sub(%r{/$}, "")
      @connection = connection || Faraday.new(url: "#{@base_url}/") do |f|
        f.request :json
        f.adapter Faraday.default_adapter
      end
      @treat_valid_license_as_addon = treat_valid_license_as_addon
    end

    def check(tenant_id)
      payload = post("addons/whatsapp/entitlement", tenant_id: tenant_id, client_id: "")
      from_payload(tenant_id, payload, "addons/whatsapp/entitlement")
    rescue NotFoundError
      license = post("licenses/verify", client_id: "", tenant_id: tenant_id)
      from_license(tenant_id, license)
    end

    def self.assert!(checker, tenant_id, skip:)
      return { enabled: true, tenant_id: tenant_id, add_on: "whatsapp", plan: "demo", message: "skipEntitlementCheck" } if skip
      if checker.nil?
        raise EntitlementError.new(
          "EntitlementChecker ausente: injete StaticEntitlementChecker (demo) ou OrdexPayEntitlementChecker",
          tenant_id: tenant_id
        )
      end
      result = symbolize(checker.check(tenant_id))
      unless result[:enabled]
        raise EntitlementError.new(
          result[:message] || "Add-on WhatsApp do plano SaaS Ordex Pay nao esta habilitado para este tenant",
          tenant_id: tenant_id
        )
      end
      result
    end

    private

    def post(path, body)
      resposta = @connection.post(path) do |req|
        req.headers["Accept"] = "application/json"
        req.headers["Content-Type"] = "application/json"
        req.headers["chave_api"] = @api_key
        req.headers["X-Api-Key"] = @api_key
        req.body = JSON.dump(body)
      end
      payload = decode(resposta.body)
      return payload if resposta.success?

      rec = payload.is_a?(Hash) ? payload : {}
      mensagem = rec["message"] || rec["error"] || "Erro HTTP #{resposta.status}"
      raise OrdexWhatsApp.error_from_status(resposta.status, mensagem, body: payload)
    end

    def from_payload(tenant_id, payload, source)
      rec = as_hash(payload)
      dados = unwrap(payload)
      enabled = truthy?(dados["enabled"]) || truthy?(dados["valid"]) || truthy?(rec["enabled"]) ||
                truthy?(rec["valid"]) || addon_flag?(dados) || addon_flag?(rec)
      {
        enabled: enabled,
        tenant_id: tenant_id,
        add_on: "whatsapp",
        plan: dados["plan"] || rec["plan"] || "saas",
        message: dados["message"] || rec["message"] || source,
        raw: payload
      }
    end

    def from_license(tenant_id, payload)
      rec = as_hash(payload)
      dados = unwrap(payload)
      license_valid = truthy?(dados["valid"]) || truthy?(rec["valid"]) || truthy?(rec["success"])
      addon = addon_flag?(dados) || addon_flag?(rec)
      enabled = addon || (@treat_valid_license_as_addon && license_valid)
      {
        enabled: enabled,
        tenant_id: tenant_id,
        add_on: "whatsapp",
        plan: "saas",
        message: enabled ? "add-on whatsapp autorizado via licenses/verify" : "licenses/verify sem add-on whatsapp habilitado",
        raw: payload
      }
    end

    def unwrap(payload)
      rec = as_hash(payload)
      data = rec["data"]
      data.is_a?(Hash) ? data : rec
    end

    def as_hash(payload)
      payload.is_a?(Hash) ? payload : {}
    end

    def addon_flag?(rec)
      return false unless rec.is_a?(Hash)
      return true if truthy?(rec["whatsapp_addon"]) || truthy?(rec["addon_whatsapp"]) || truthy?(rec["whatsapp"])

      addons = rec["addons"]
      return false unless addons.is_a?(Hash)

      wa = addons["whatsapp"]
      return true if truthy?(wa)
      return truthy?(wa["enabled"]) || truthy?(wa["valid"]) if wa.is_a?(Hash)

      false
    end

    def truthy?(value)
      value == true || value == "true" || value == 1 || value == "1"
    end

    def decode(corpo)
      return {} if corpo.nil? || corpo.to_s.strip.empty?

      JSON.parse(corpo)
    rescue JSON::ParserError
      { "raw" => corpo }
    end

    def self.symbolize(hash)
      return hash unless hash.is_a?(Hash)

      hash.each_with_object({}) { |(k, v), acc| acc[k.to_sym] = v }
    end
  end
end
