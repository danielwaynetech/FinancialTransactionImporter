export interface Transaction {
  id: number;
  transactionTime: string;
  amount: number;
  description: string;
  transactionId: string;
  createdAt: string;
  updatedAt: string;
}

export interface PaginatedResponse {
  items: Transaction[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface UploadResponse {
  message: string;
}

/** Mirrors the server-side ValidationError record */
export interface ValidationError {
  row: number | null;
  column: string | null;
  message: string;
}

/**
 * RFC 7807 ProblemDetails shape returned by the API for all error responses.
 * The `errors` extension field is present on 422 validation failures.
 */
export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail: string;
  instance: string;
  errors?: ValidationError[];
  errorCount?: number;
}

export interface UpdateTransactionPayload {
  transactionTime: string;
  amount: number;
  description: string;
}
