# frozen_string_literal: true

require "faraday"
require "json"

module OrdexWhatsApp
  class GraphClient
    def initialize(access_token:, phone_number_id:, waba_id: nil, graph_version: nil, graph_base_url: nil, connection: nil)
      version = graph_version.to_s.strip.empty? ? DEFAULT_GRAPH_VERSION : graph_version
      root = (graph_base_url.to_s.strip.empty? ? DEFAULT_GRAPH_BASE_URL : graph_base_url).sub(%r{/$}, "")
      @access_token = access_token
      @phone_number_id = phone_number_id
      @waba_id = waba_id
      @connection = connection || Faraday.new(url: "#{root}/#{version}/") do |f|
        f.request :json
        f.adapter Faraday.default_adapter
      end
    end

    def send_message(input)
      body = self.class.build_message_body(input)
      payload = request(:post, "#{@phone_number_id}/messages", body)
      self.class.parse_message_result(payload)
    end

    def self.build_message_body(input)
      input = stringify(input)
      body = {
        "messaging_product" => "whatsapp",
        "recipient_type" => "individual",
        "to" => input["to"],
        "type" => input["type"]
      }
      callback = callback_data(input)
      body["biz_opaque_callback_data"] = callback if callback

      if input["type"] == "template"
        template = stringify(input["template"])
        raise ConfigurationError, "template.name e obrigatorio para envio de template" if template["name"].to_s.strip.empty?

        body["template"] = {
          "name" => template["name"],
          "language" => { "code" => template["language"].to_s.empty? ? "pt_BR" : template["language"] },
          "components" => template["components"]
        }.compact
        return body
      end

      if input["type"] == "text"
        text = stringify(input["text"])
        raise ConfigurationError, "text.body e obrigatorio para mensagem de sessao" if text["body"].to_s.strip.empty?

        body["text"] = { "preview_url" => !!text["preview_url"], "body" => text["body"] }
        return body
      end

      media = stringify(input["media"])
      raise ConfigurationError, "media.id ou media.link e obrigatorio" if media["id"].to_s.empty? && media["link"].to_s.empty?

      media_body = {}
      media_body["id"] = media["id"] if media["id"]
      media_body["link"] = media["link"] if media["link"]
      media_body["caption"] = media["caption"] if media["caption"]
      media_body["filename"] = media["filename"] if media["filename"]
      body[input["type"]] = media_body
      body
    end

    def self.parse_message_result(payload)
      rec = payload.is_a?(Hash) ? payload : {}
      {
        messaging_product: rec["messaging_product"],
        contacts: Array(rec["contacts"]).map { |c| { input: c["input"], wa_id: c["wa_id"] } },
        messages: Array(rec["messages"]).map { |m| { id: m["id"].to_s, message_status: m["message_status"] } },
        raw: payload
      }
    end

    def self.callback_data(input)
      return input["callback_data"].to_s[0, 512] if input["callback_data"]

      payload = JSON.dump({ "category" => input["category"] })
      payload.length <= 512 ? payload : nil
    end

    def self.stringify(hash)
      return {} if hash.nil?
      return hash.transform_keys(&:to_s) if hash.is_a?(Hash)

      {}
    end

    private

    def request(method, path, body = nil)
      resposta = @connection.run_request(method, path, nil, {}) do |req|
        req.headers["Authorization"] = "Bearer #{@access_token}"
        req.headers["Accept"] = "application/json"
        if body
          req.headers["Content-Type"] = "application/json"
          req.body = JSON.dump(body)
        end
      end
      payload = decode(resposta.body)
      return payload if resposta.success?

      rec = payload.is_a?(Hash) ? payload : {}
      err = rec["error"].is_a?(Hash) ? rec["error"] : {}
      mensagem = err["message"] || rec["message"] || "Erro HTTP #{resposta.status} na Graph API"
      raise OrdexWhatsApp.error_from_status(resposta.status, mensagem, body: payload, errors: rec["error"])
    end

    def decode(corpo)
      return {} if corpo.nil? || corpo.to_s.strip.empty?

      JSON.parse(corpo)
    rescue JSON::ParserError
      { "raw" => corpo }
    end
  end
end
