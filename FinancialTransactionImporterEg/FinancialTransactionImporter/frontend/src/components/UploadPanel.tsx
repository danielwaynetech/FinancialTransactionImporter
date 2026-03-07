import { useState, useRef, DragEvent } from 'react';
import { uploadCsv, extractProblemDetails, extractFallbackMessage } from '../services/api';
import type { ProblemDetails, ValidationError } from '../types';

interface Props {
  onSuccess: () => void;
}

type UploadState = 'idle' | 'uploading' | 'success' | 'error';

/** Groups validation errors by row number for cleaner display */
function groupByRow(errors: ValidationError[]): Map<string, ValidationError[]> {
  const map = new Map<string, ValidationError[]>();
  for (const err of errors) {
    const key = err.row != null ? `Row ${err.row}` : 'File';
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(err);
  }
  return map;
}

export function UploadPanel({ onSuccess }: Props) {
  const [state, setState] = useState<UploadState>('idle');
  const [problem, setProblem] = useState<ProblemDetails | null>(null);
  const [fallbackMsg, setFallbackMsg] = useState<string>('');
  const [successMsg, setSuccessMsg] = useState('');
  const [isDragging, setIsDragging] = useState(false);
  const [fileName, setFileName] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const handleFile = async (file: File) => {
    setFileName(file.name);
    setState('uploading');
    setProblem(null);
    setFallbackMsg('');
    setSuccessMsg('');
    try {
      const msg = await uploadCsv(file);
      setState('success');
      setSuccessMsg(msg);
      onSuccess();
    } catch (err) {
      setState('error');
      const pd = extractProblemDetails(err);
      if (pd) {
        setProblem(pd);
      } else {
        setFallbackMsg(extractFallbackMessage(err));
      }
    }
  };

  const onInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) handleFile(file);
    e.target.value = '';
  };

  const onDrop = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setIsDragging(false);
    const file = e.dataTransfer.files?.[0];
    if (file) handleFile(file);
  };

  const reset = () => {
    setState('idle');
    setProblem(null);
    setFallbackMsg('');
    setSuccessMsg('');
    setFileName(null);
  };

  const validationErrors = problem?.errors ?? [];
  const groupedErrors = groupByRow(validationErrors);
  const errorCount = problem?.errorCount ?? validationErrors.length;

  return (
    <div className="upload-panel">
      <div className="panel-header">
        <span className="panel-tag">01</span>
        <h2>Import Transactions</h2>
      </div>

      <div
        className={`drop-zone ${isDragging ? 'dragging' : ''} ${state === 'uploading' ? 'uploading' : ''}`}
        onClick={() => state !== 'uploading' && inputRef.current?.click()}
        onDragOver={(e) => { e.preventDefault(); setIsDragging(true); }}
        onDragLeave={() => setIsDragging(false)}
        onDrop={onDrop}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => e.key === 'Enter' && inputRef.current?.click()}
        aria-label="CSV file upload area"
      >
        <input
          ref={inputRef}
          type="file"
          accept=".csv"
          onChange={onInputChange}
          style={{ display: 'none' }}
          aria-hidden="true"
        />
        {state === 'uploading' ? (
          <div className="drop-zone-content">
            <div className="spinner" />
            <p className="dz-label">Processing <span className="mono">{fileName}</span>…</p>
          </div>
        ) : (
          <div className="drop-zone-content">
            <div className="dz-icon">
              <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
                <path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4" />
                <polyline points="17 8 12 3 7 8" />
                <line x1="12" y1="3" x2="12" y2="15" />
              </svg>
            </div>
            <p className="dz-label">Drop a CSV file here, or <span className="dz-link">browse</span></p>
            <p className="dz-hint">Columns: TransactionTime · Amount · Description · TransactionId</p>
          </div>
        )}
      </div>

      {/* ── Success ── */}
      {state === 'success' && (
        <div className="alert alert-success" role="alert">
          <span className="alert-icon">✓</span>
          <div>
            <strong>Import successful</strong>
            <p>{successMsg}</p>
          </div>
          <button className="alert-dismiss" onClick={reset} aria-label="Dismiss">×</button>
        </div>
      )}

      {/* ── Structured ProblemDetails error (validation or duplicate) ── */}
      {state === 'error' && problem && (
        <div className="alert alert-error" role="alert">
          <span className="alert-icon">✕</span>
          <div className="alert-body">
            <strong>{problem.title}</strong>
            <p className="problem-detail">{problem.detail}</p>

            {/* Validation errors grouped by row */}
            {groupedErrors.size > 0 && (
              <div className="error-group-list">
                <p className="error-group-summary mono">
                  {errorCount} error{errorCount !== 1 ? 's' : ''} across {groupedErrors.size} location{groupedErrors.size !== 1 ? 's' : ''}
                </p>
                {Array.from(groupedErrors.entries()).map(([rowLabel, rowErrors]) => (
                  <div key={rowLabel} className="error-group">
                    <span className="error-group-row mono">{rowLabel}</span>
                    <ul className="error-list">
                      {rowErrors.map((e, i) => (
                        <li key={i}>
                          {e.column && (
                            <span className="error-column mono">{e.column}: </span>
                          )}
                          {e.message}
                        </li>
                      ))}
                    </ul>
                  </div>
                ))}
              </div>
            )}
          </div>
          <button className="alert-dismiss" onClick={reset} aria-label="Dismiss">×</button>
        </div>
      )}

      {/* ── Fallback for non-ProblemDetails errors (network failure, etc.) ── */}
      {state === 'error' && !problem && fallbackMsg && (
        <div className="alert alert-error" role="alert">
          <span className="alert-icon">✕</span>
          <div className="alert-body">
            <strong>Upload failed</strong>
            <p>{fallbackMsg}</p>
          </div>
          <button className="alert-dismiss" onClick={reset} aria-label="Dismiss">×</button>
        </div>
      )}
    </div>
  );
}
