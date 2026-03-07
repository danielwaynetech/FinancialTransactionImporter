using TransactionImporter.Core.Entities;
using TransactionImporter.Core.Models;

namespace TransactionImporter.Core.Interfaces;

public interface ITransactionRepository
{
    Task<IEnumerable<string>> GetExistingTransactionIdsAsync(IEnumerable<string> transactionIds);
    Task AddRangeAsync(IEnumerable<Transaction> transactions);
    Task<PaginatedResult<Transaction>> GetPaginatedAsync(int pageNumber, int pageSize);
    Task<Transaction?> GetByIdAsync(int id);
    Task UpdateAsync(Transaction transaction);
    Task DeleteAsync(int id);
    Task SaveChangesAsync();
}
