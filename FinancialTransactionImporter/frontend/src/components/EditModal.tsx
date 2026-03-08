import { useState, useEffect } from 'react';
import type { Transaction, UpdateTransactionPayload } from '../types';
import { extractProblemDetails, extractFallbackMessage } from '../services/api';

interface Props {
  transaction: Transaction;
  onSave: (id: number, payload: UpdateTransactionPayload) => Promise<void>;
  onClose: () => void;
}

export function EditModal({ transaction, onSave, onClose }: Props) {
  const [transactionTime, setTransactionTime] = useState('');
  const [amount, setAmount] = useState('');
  const [description, setDescription] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // Format as datetime-local value: YYYY-MM-DDTHH:mm:ss
    const dt = new Date(transaction.transactionTime);
    const pad = (n: number) => String(n).padStart(2, '0');
    const local = `${dt.getFullYear()}-${pad(dt.getMonth() + 1)}-${pad(dt.getDate())}T${pad(dt.getHours())}:${pad(dt.getMinutes())}:${pad(dt.getSeconds())}`;
    setTransactionTime(local);
    setAmount(transaction.amount.toFixed(2));
    setDescription(transaction.description);
  }, [transaction]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const parsedAmount = parseFloat(amount);
    if (isNaN(parsedAmount)) {
      setError('Amount must be a valid number.');
      return;
    }
    if (!/^\d+\.\d{2}$/.test(amount.replace(/^-/, ''))) {
      setError('Amount must have exactly 2 decimal places (e.g. 123.45).');
      return;
    }
    if (!description.trim()) {
      setError('Description cannot be empty.');
      return;
    }

    setSaving(true);
    try {
      await onSave(transaction.id, {
        transactionTime: new Date(transactionTime).toISOString(),
        amount: parsedAmount,
        description: description.trim(),
      });
      onClose();
    } catch (err) {
      const pd = extractProblemDetails(err);
      setError(pd ? pd.detail : extractFallbackMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="modal-backdrop" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal" role="dialog" aria-modal="true" aria-labelledby="modal-title">
        <div className="modal-header">
          <h3 id="modal-title">Edit Transaction</h3>
          <p className="modal-sub mono">{transaction.transactionId}</p>
          <button className="modal-close" onClick={onClose} aria-label="Close">×</button>
        </div>

        <form onSubmit={handleSubmit} className="modal-body" noValidate>
          <div className="field">
            <label htmlFor="edit-time">Transaction Time</label>
            <input
              id="edit-time"
              type="datetime-local"
              step="1"
              value={transactionTime}
              onChange={(e) => setTransactionTime(e.target.value)}
              required
            />
          </div>

          <div className="field">
            <label htmlFor="edit-amount">Amount</label>
            <input
              id="edit-amount"
              type="text"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              placeholder="123.45"
              required
            />
          </div>

          <div className="field">
            <label htmlFor="edit-desc">Description</label>
            <input
              id="edit-desc"
              type="text"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              required
            />
          </div>

          {error && <div className="field-error">{error}</div>}

          <div className="modal-actions">
            <button type="button" className="btn-ghost" onClick={onClose} disabled={saving}>
              Cancel
            </button>
            <button type="submit" className="btn-primary" disabled={saving}>
              {saving ? 'Saving…' : 'Save Changes'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
