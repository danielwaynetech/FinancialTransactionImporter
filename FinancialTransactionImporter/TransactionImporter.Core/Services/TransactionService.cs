using TransactionImporter.Core.Entities;
using TransactionImporter.Core.Interfaces;
using TransactionImporter.Core.Models;

namespace TransactionImporter.Core.Services
{
    /// <summary>
    /// Orchestrates the import, retrieval, update, and deletion of financial transactions.
    /// </summary>
    public class TransactionService : ITransactionService
    {
        private readonly ITransactionRepository _repository;
        private readonly ICsvParserService _csvParser;

        public TransactionService(ITransactionRepository repository, ICsvParserService csvParser)
        {
            _repository = repository;
            _csvParser = csvParser;
        }

        public async Task<(bool Success, List<ValidationError> Errors)> ImportAsync(Stream csvStream)
        {
            var validationResult = _csvParser.ParseAndValidate(csvStream);

            if (!validationResult.IsValid)
                return (false, validationResult.Errors);

            // Build a row-number index so DB duplicate errors reference the originating row.
            var idToRow = validationResult.ParsedTransactions
                .Select((t, index) => (t.TransactionId, RowNumber: index + 2)) // +2: row 1 = header
                .ToDictionary(x => x.TransactionId, x => x.RowNumber, StringComparer.OrdinalIgnoreCase);

            var incomingIds = idToRow.Keys;
            var existingIds = await _repository.GetExistingTransactionIdsAsync(incomingIds);

            var duplicateErrors = existingIds
                .Select(id => ValidationError.Row(
                    idToRow[id],
                    "TransactionId",
                    $"TransactionId '{id}' already exists in the database and cannot be imported again."))
                .ToList();

            if (duplicateErrors.Count > 0)
                return (false, duplicateErrors);

            await _repository.AddRangeAsync(validationResult.ParsedTransactions);
            await _repository.SaveChangesAsync();

            return (true, []);
        }

        public async Task<PaginatedResult<Transaction>> GetTransactionsAsync(int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 200);
            return await _repository.GetPaginatedAsync(pageNumber, pageSize);
        }

        public async Task<(bool Success, string? Error, NotFoundReason Reason)> UpdateTransactionAsync(
            int id, UpdateTransactionRequest request)
        {
            var transaction = await _repository.GetByIdAsync(id);

            if (transaction is null)
                return (false, $"No transaction with ID {id} exists.", NotFoundReason.NeverExisted);

            transaction.TransactionTime = request.TransactionTime;
            transaction.Amount = request.Amount;
            transaction.Description = request.Description;
            transaction.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(transaction);
            await _repository.SaveChangesAsync();

            return (true, null, NotFoundReason.None);
        }

        public async Task<(bool Success, string? Error, NotFoundReason Reason)> DeleteTransactionAsync(int id)
        {
            var transaction = await _repository.GetByIdAsync(id);

            if (transaction is null)
                return (false, $"No transaction with ID {id} exists.", NotFoundReason.NeverExisted);

            await _repository.DeleteAsync(id);
            await _repository.SaveChangesAsync();

            return (true, null, NotFoundReason.None);
        }
    }
}
