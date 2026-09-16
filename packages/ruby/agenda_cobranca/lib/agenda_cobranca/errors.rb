# frozen_string_literal: true

module AgendaCobranca
  class Error < StandardError; end
  class ConfigurationError < Error; end
  class SignatureError < Error; end

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
end
