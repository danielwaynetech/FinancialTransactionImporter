import axios, { AxiosError } from 'axios';
import type { PaginatedResponse, ProblemDetails, UpdateTransactionPayload, ValidationError } from '../types';

const API_BASE = import.meta.env.VITE_API_URL ?? '/api';
const API_KEY  = import.meta.env.VITE_API_KEY  ?? 'dev-api-key-change-me-in-production';

const client = axios.create({
  baseURL: API_BASE,
  headers: { 'X-Api-Key': API_KEY },
});

export async function uploadCsv(file: File): Promise<string> {
  const form = new FormData();
  form.append('file', file);
  const response = await client.post<{ message: string }>('/transactions/upload', form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return response.data.message;
}

export async function getTransactions(
  pageNumber: number,
  pageSize: number
): Promise<PaginatedResponse> {
  const response = await client.get<PaginatedResponse>('/transactions', {
    params: { pageNumber, pageSize },
  });
  return response.data;
}

export async function updateTransaction(
  id: number,
  payload: UpdateTransactionPayload
): Promise<void> {
  await client.put(`/transactions/${id}`, payload);
}

export async function deleteTransaction(id: number): Promise<void> {
  await client.delete(`/transactions/${id}`);
}

/**
 * Extracts a ProblemDetails object from an Axios error response.
 * Returns null if the response is not a ProblemDetails shape.
 */
export function extractProblemDetails(err: unknown): ProblemDetails | null {
  if (err instanceof AxiosError && err.response?.data) {
    const data = err.response.data as Partial<ProblemDetails>;
    // RFC 7807 responses always have `type` and `title`
    if (data.type && data.title) {
      return data as ProblemDetails;
    }
  }
  return null;
}

/**
 * Extracts the structured validation error list from a ProblemDetails response.
 * Returns an empty array if not a validation failure or no errors present.
 */
export function extractValidationErrors(err: unknown): ValidationError[] {
  const problem = extractProblemDetails(err);
  return problem?.errors ?? [];
}

/**
 * Falls back to a single human-readable string for non-ProblemDetails errors
 * (network failures, unexpected shapes, etc.)
 */
export function extractFallbackMessage(err: unknown): string {
  const problem = extractProblemDetails(err);
  if (problem) return problem.detail ?? problem.title;
  if (err instanceof Error) return err.message;
  return 'An unexpected error occurred.';
}
