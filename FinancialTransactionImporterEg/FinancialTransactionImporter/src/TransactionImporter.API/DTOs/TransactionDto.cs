using TransactionImporter.Core.Entities;

namespace TransactionImporter.API.DTOs;

/// <summary>
/// API response shape for a single transaction record.
/// </summary>
public record TransactionDto(
    int Id,
    DateTime TransactionTime,
    decimal Amount,
    string Description,
    string TransactionId,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static TransactionDto FromEntity(Transaction t) => new(
        t.Id,
        t.TransactionTime,
        t.Amount,
        t.Description,
        t.TransactionId,
        t.CreatedAt,
        t.UpdatedAt);
}
