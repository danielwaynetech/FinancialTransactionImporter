
using TransactionImporter.Core.Entities;

namespace TransactionImporter.Core.Models
{
    public class CsvValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationError> Errors { get; set; } = [];
        public List<Transaction> ParsedTransactions { get; set; } = [];
    }
}
