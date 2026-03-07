import { UploadPanel } from './components/UploadPanel';
import { TransactionTable } from './components/TransactionTable';
import { useTransactions } from './hooks/useTransactions';
import './styles.css';

export default function App() {
  const { transactions, pagination, loading, error, setPage, refresh, remove, update } = useTransactions(20);

  return (
    <div className="app">
      <header className="app-header">
        <div className="header-inner">
          <div className="logo">
            <span className="logo-mark">Σ</span>
            <div>
              <span className="logo-title">Transaction Importer</span>
              <span className="logo-sub">Financial Data Pipeline</span>
            </div>
          </div>
          <div className="header-badge mono">v1.0</div>
        </div>
      </header>

      <main className="app-main">
        <UploadPanel onSuccess={refresh} />
        <TransactionTable
          transactions={transactions}
          pagination={pagination}
          loading={loading}
          error={error}
          onPageChange={setPage}
          onDelete={remove}
          onUpdate={update}
        />
      </main>

      <footer className="app-footer">
        <span className="mono">Financial Transaction Importer</span>
      </footer>
    </div>
  );
}
