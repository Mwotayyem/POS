// Shared types mirroring the backend unified response envelope and error shape.

/** A field-level validation error. */
export interface FieldError {
  field: string;
  message: string;
}

/** The machine-readable error object returned by the API. */
export interface ApiError {
  code: string;
  message: string;
  details?: FieldError[] | null;
}

/** Response metadata (correlation id, etc.). */
export interface ApiMeta {
  correlationId?: string;
  [key: string]: unknown;
}

/** The unified response envelope: { success, data, error, meta }. */
export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  error: ApiError | null;
  meta: ApiMeta | null;
}

/** Thrown by the API client when a request fails; carries the parsed ApiError. */
export class ApiRequestError extends Error {
  readonly code: string;
  readonly details?: FieldError[] | null;
  readonly status: number;

  constructor(error: ApiError, status: number) {
    super(error.message);
    this.name = 'ApiRequestError';
    this.code = error.code;
    this.details = error.details;
    this.status = status;
  }
}
