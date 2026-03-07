using TransactionImporter.Core.Entities;
using TransactionImporter.Core.Models;

namespace TransactionImporter.Core.Interfaces;

public interface ITransactionService
{
    Task<(bool Success, List<ValidationError> Errors)> ImportAsync(Stream csvStream);
    Task<PaginatedResult<Transaction>> GetTransactionsAsync(int pageNumber, int pageSize);
    Task<(bool Success, string? Error, NotFoundReason Reason)> UpdateTransactionAsync(int id, UpdateTransactionRequest request);
    Task<(bool Success, string? Error, NotFoundReason Reason)> DeleteTransactionAsync(int id);
}

public enum NotFoundReason
{
    None,
    NeverExisted,
}
