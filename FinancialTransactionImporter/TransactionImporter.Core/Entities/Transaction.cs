
namespace TransactionImporter.Core.Entities
{
    public class Transaction
    {
        public int Id { get; set; }
        public DateTime TransactionTime { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // ── Soft delete ────────────────────────────────────────────────────────
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
