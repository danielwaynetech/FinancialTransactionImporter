using Microsoft.EntityFrameworkCore;
using TransactionImporter.Core.Entities;
using TransactionImporter.Core.Interfaces;
using TransactionImporter.Core.Models;
using TransactionImporter.Infrastructure.Data;

namespace TransactionImporter.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of the transaction repository.
/// Deletions are soft — records are flagged with IsDeleted rather than
/// removed from the database. The global query filter on AppDbContext
/// ensures soft-deleted records are excluded from all standard queries.
/// </summary>
public class TransactionRepository : ITransactionRepository
{
    private readonly AppDbContext _context;

    public TransactionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<string>> GetExistingTransactionIdsAsync(IEnumerable<string> transactionIds)
    {
        var idList = transactionIds.ToList();
        return await _context.Transactions
            .Where(t => idList.Contains(t.TransactionId))
            .Select(t => t.TransactionId)
            .ToListAsync();
    }

    public async Task AddRangeAsync(IEnumerable<Transaction> transactions)
    {
        await _context.Transactions.AddRangeAsync(transactions);
    }

    public async Task<PaginatedResult<Transaction>> GetPaginatedAsync(int pageNumber, int pageSize)
    {
        var query = _context.Transactions
            .OrderByDescending(t => t.TransactionTime);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResult<Transaction>
        {
            Items      = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize   = pageSize
        };
    }

    public async Task<Transaction?> GetByIdAsync(int id)
    {
        return await _context.Transactions.FindAsync(id);
    }

    public Task UpdateAsync(Transaction transaction)
    {
        _context.Transactions.Update(transaction);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Soft-deletes the transaction by setting IsDeleted and recording the
    /// deletion timestamp. The record remains in the database for audit purposes
    /// but is excluded from all standard queries via the global query filter.
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var transaction = await _context.Transactions.FindAsync(id);
        if (transaction is null)
            return;

        transaction.IsDeleted = true;
        transaction.DeletedAt = DateTime.UtcNow;
        _context.Transactions.Update(transaction);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
