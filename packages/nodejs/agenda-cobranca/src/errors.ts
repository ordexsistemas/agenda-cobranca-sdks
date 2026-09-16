export class AgendaCobrancaError extends Error {
  constructor(message: string) {
    super(message);
    this.name = new.target.name;
  }
}

export class ConfigurationError extends AgendaCobrancaError {}

export class SignatureError extends AgendaCobrancaError {}

export class ApiError extends AgendaCobrancaError {
  readonly status: number;
  readonly body: unknown;
  readonly errors: unknown;

  constructor(message: string, status: number, body: unknown = null, errors: unknown = null) {
    super(message);
    this.status = status;
    this.body = body;
    this.errors = errors;
  }
}

export class AuthenticationError extends ApiError {}
export class NotFoundError extends ApiError {}
export class ValidationError extends ApiError {}
export class RateLimitError extends ApiError {}

export function errorFromStatus(
  status: number,
  message: string,
  body: unknown,
  errors: unknown,
): ApiError {
  switch (status) {
    case 401:
    case 403:
      return new AuthenticationError(message, status, body, errors);
    case 404:
      return new NotFoundError(message, status, body, errors);
    case 422:
      return new ValidationError(message, status, body, errors);
    case 429:
      return new RateLimitError(message, status, body, errors);
    default:
      return new ApiError(message, status, body, errors);
  }
}
