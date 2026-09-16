# frozen_string_literal: true

require "openssl"

module OrdexWhatsApp
  module Webhooks
    module_function

    def verify_challenge(query, verify_token)
      raise ConfigurationError, "WHATSAPP_VERIFY_TOKEN e obrigatorio" if verify_token.to_s.strip.empty?

      query = stringify(query)
      mode = query["hub.mode"] || query["hub_mode"]
      token = query["hub.verify_token"] || query["hub_verify_token"]
      challenge = query["hub.challenge"] || query["hub_challenge"] || ""
      return challenge.to_s if mode == "subscribe" && token == verify_token

      raise ValidationError.new("Token de verificacao de webhook invalido", status: 403)
    end

    def signature_header(raw_body, app_secret)
      hex = OpenSSL::HMAC.hexdigest("SHA256", app_secret, raw_body)
      "sha256=#{hex}"
    end

    def verify_signature(raw_body, signature_header, app_secret)
      raise ConfigurationError, "WHATSAPP_APP_SECRET e obrigatorio" if app_secret.to_s.strip.empty?
      return false if signature_header.nil? || signature_header.empty?

      expected = signature_header(raw_body, app_secret)
      return false unless expected.bytesize == signature_header.bytesize

      OpenSSL.fixed_length_secure_compare(expected, signature_header)
    end

    def verify_signature!(raw_body, signature_header, app_secret)
      return true if verify_signature(raw_body, signature_header, app_secret)

      raise SignatureError, "Assinatura X-Hub-Signature-256 invalida"
    end

    def parse_inbound_messages(payload)
      out = []
      return out unless payload.is_a?(Hash)

      Array(payload["entry"] || payload[:entry]).each do |entry|
        next unless entry.is_a?(Hash)

        Array(entry["changes"] || entry[:changes]).each do |change|
          next unless change.is_a?(Hash)

          value = change["value"] || change[:value] || {}
          Array(value["messages"] || value[:messages]).each do |msg|
            next unless msg.is_a?(Hash)

            text = msg["text"] || msg[:text]
            out << {
              from: (msg["from"] || msg[:from]).to_s,
              id: (msg["id"] || msg[:id]).to_s,
              timestamp: (msg["timestamp"] || msg[:timestamp]).to_s,
              type: (msg["type"] || msg[:type]).to_s,
              text: text.is_a?(Hash) ? (text["body"] || text[:body]) : nil,
              callback_data: msg["biz_opaque_callback_data"] || msg[:biz_opaque_callback_data],
              raw: msg
            }
          end
        end
      end
      out
    end

    def stringify(hash)
      return {} if hash.nil?

      hash.each_with_object({}) { |(k, v), acc| acc[k.to_s] = v }
    end
  end
end
