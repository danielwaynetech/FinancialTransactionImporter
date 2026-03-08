using System.ComponentModel.DataAnnotations;

namespace TransactionImporter.Core.Models
{
    public class UpdateTransactionRequest
    {
        [Required]
        public DateTime TransactionTime { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "Description cannot be empty.")]
        public string Description { get; set; } = string.Empty;
    }
}
