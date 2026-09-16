# frozen_string_literal: true

module OrdexWhatsApp
  class Error < StandardError; end
  class ConfigurationError < Error; end
  class SignatureError < Error; end

  class EntitlementError < Error
    attr_reader :tenant_id

    def initialize(message, tenant_id:)
      super(message)
      @tenant_id = tenant_id
    end
  end

  class QuotaExceededError < Error
    attr_reader :category, :used, :quota, :period

    def initialize(message, category:, used:, quota:, period:)
      super(message)
      @category = category
      @used = used
      @quota = quota
      @period = period
    end
  end

  class ApiError < Error
    attr_reader :status, :body, :errors

    def initialize(message, status:, body: nil, errors: nil)
      super(message)
      @status = status
      @body = body
      @errors = errors
    end
  end

  class AuthenticationError < ApiError; end
  class NotFoundError < ApiError; end
  class ValidationError < ApiError; end
  class RateLimitError < ApiError; end

  def self.error_from_status(status, message, body: nil, errors: nil)
    klass = case status
            when 401, 403 then AuthenticationError
            when 404 then NotFoundError
            when 422 then ValidationError
            when 429 then RateLimitError
            else ApiError
            end
    klass.new(message, status: status, body: body, errors: errors)
  end
end
