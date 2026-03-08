import { useState, useEffect, useCallback } from 'react';
import { getTransactions, deleteTransaction, updateTransaction, extractFallbackMessage } from '../services/api';
import type { Transaction, PaginatedResponse, UpdateTransactionPayload } from '../types';

export function useTransactions(pageSize = 20) {
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PaginatedResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (pageNumber: number) => {
    setLoading(true);
    setError(null);
    try {
      const result = await getTransactions(pageNumber, pageSize);
      setData(result);
      setPage(pageNumber);
    } catch (err) {
      setError(extractFallbackMessage(err));
    } finally {
      setLoading(false);
    }
  }, [pageSize]);

  useEffect(() => { load(page); }, [load, page]);

  const refresh = useCallback(() => load(page), [load, page]);

  const remove = useCallback(async (id: number) => {
    await deleteTransaction(id);
    await load(page);
  }, [load, page]);

  const update = useCallback(async (id: number, payload: UpdateTransactionPayload) => {
    await updateTransaction(id, payload);
    await load(page);
  }, [load, page]);

  return {
    transactions: data?.items ?? [] as Transaction[],
    pagination: data,
    loading,
    error,
    page,
    setPage: (p: number) => load(p),
    refresh,
    remove,
    update,
  };
}
