import { useState } from 'react';
import type { Transaction, PaginatedResponse, UpdateTransactionPayload } from '../types';
import { EditModal } from './EditModal';

interface Props {
  transactions: Transaction[];
  pagination: PaginatedResponse | null;
  loading: boolean;
  error: string | null;
  onPageChange: (page: number) => void;
  onDelete: (id: number) => Promise<void>;
  onUpdate: (id: number, payload: UpdateTransactionPayload) => Promise<void>;
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleString('en-ZA', {
      year: 'numeric', month: 'short', day: '2-digit',
      hour: '2-digit', minute: '2-digit', second: '2-digit',
    });
  } catch {
    return iso;
  }
}

function formatAmount(n: number): string {
  return new Intl.NumberFormat('en-ZA', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(n);
}

export function TransactionTable({ transactions, pagination, loading, error, onPageChange, onDelete, onUpdate }: Props) {
  const [editing, setEditing] = useState<Transaction | null>(null);
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState<number | null>(null);

  const handleDelete = async (id: number) => {
    setDeletingId(id);
    try {
      await onDelete(id);
    } finally {
      setDeletingId(null);
      setDeleteConfirm(null);
    }
  };

  const totalPages = pagination?.totalPages ?? 0;
  const currentPage = pagination?.pageNumber ?? 1;

  const pageNumbers = (): (number | '…')[] => {
    if (totalPages <= 7) return Array.from({ length: totalPages }, (_, i) => i + 1);
    const pages: (number | '…')[] = [1];
    if (currentPage > 3) pages.push('…');
    for (let i = Math.max(2, currentPage - 1); i <= Math.min(totalPages - 1, currentPage + 1); i++) pages.push(i);
    if (currentPage < totalPages - 2) pages.push('…');
    pages.push(totalPages);
    return pages;
  };

  return (
    <div className="table-panel">
      <div className="panel-header">
        <span className="panel-tag">02</span>
        <h2>Transaction Records</h2>
        {pagination && (
          <span className="record-count mono">
            {pagination.totalCount.toLocaleString()} records
          </span>
        )}
      </div>

      {error && (
        <div className="alert alert-error" style={{ marginBottom: '1rem' }}>
          <span className="alert-icon">!</span>
          <span>{error}</span>
        </div>
      )}

      <div className="table-wrap">
        <table className="data-table" aria-label="Transaction records">
          <thead>
            <tr>
              <th>Transaction ID</th>
              <th>Time</th>
              <th className="col-right">Amount</th>
              <th>Description</th>
              <th className="col-center">Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={5} className="table-state">
                  <div className="spinner" />
                  <span>Loading…</span>
                </td>
              </tr>
            ) : transactions.length === 0 ? (
              <tr>
                <td colSpan={5} className="table-state table-empty">
                  <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1">
                    <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/><polyline points="14 2 14 8 20 8"/>
                  </svg>
                  <p>No transactions yet. Upload a CSV to get started.</p>
                </td>
              </tr>
            ) : (
              transactions.map((tx) => (
                <tr key={tx.id} className={deletingId === tx.id ? 'row-deleting' : ''}>
                  <td className="mono cell-id">{tx.transactionId}</td>
                  <td className="mono cell-date">{formatDate(tx.transactionTime)}</td>
                  <td className={`mono col-right cell-amount ${tx.amount < 0 ? 'negative' : ''}`}>
                    {formatAmount(tx.amount)}
                  </td>
                  <td className="cell-desc">{tx.description}</td>
                  <td className="col-center cell-actions">
                    {deleteConfirm === tx.id ? (
                      <div className="confirm-delete">
                        <span>Delete?</span>
                        <button
                          className="btn-danger-sm"
                          onClick={() => handleDelete(tx.id)}
                          disabled={deletingId === tx.id}
                        >
                          Yes
                        </button>
                        <button className="btn-ghost-sm" onClick={() => setDeleteConfirm(null)}>No</button>
                      </div>
                    ) : (
                      <>
                        <button
                          className="btn-icon btn-edit"
                          onClick={() => setEditing(tx)}
                          aria-label={`Edit transaction ${tx.transactionId}`}
                          title="Edit"
                        >
                          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                            <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/>
                            <path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/>
                          </svg>
                        </button>
                        <button
                          className="btn-icon btn-delete"
                          onClick={() => setDeleteConfirm(tx.id)}
                          aria-label={`Delete transaction ${tx.transactionId}`}
                          title="Delete"
                        >
                          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                            <polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a1 1 0 011-1h4a1 1 0 011 1v2"/>
                          </svg>
                        </button>
                      </>
                    )}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && (
        <div className="pagination" aria-label="Pagination">
          <button
            className="page-btn"
            onClick={() => onPageChange(currentPage - 1)}
            disabled={!pagination?.hasPreviousPage || loading}
            aria-label="Previous page"
          >
            ← Prev
          </button>

          <div className="page-numbers">
            {pageNumbers().map((p, i) =>
              p === '…' ? (
                <span key={`ellipsis-${i}`} className="page-ellipsis">…</span>
              ) : (
                <button
                  key={p}
                  className={`page-num ${p === currentPage ? 'active' : ''}`}
                  onClick={() => p !== currentPage && onPageChange(p as number)}
                  disabled={p === currentPage || loading}
                  aria-current={p === currentPage ? 'page' : undefined}
                >
                  {p}
                </button>
              )
            )}
          </div>

          <button
            className="page-btn"
            onClick={() => onPageChange(currentPage + 1)}
            disabled={!pagination?.hasNextPage || loading}
            aria-label="Next page"
          >
            Next →
          </button>
        </div>
      )}

      {editing && (
        <EditModal
          transaction={editing}
          onSave={onUpdate}
          onClose={() => setEditing(null)}
        />
      )}
    </div>
  );
}
