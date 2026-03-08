namespace TransactionImporter.Core.Models
{
    /// <summary>
    /// Represents a single, structured validation failure with optional location context.
    /// </summary>
    public class ValidationError
    {
        public int? RowNumber { get; init; }
        public string? ColumnName { get; init; }
        public string Message { get; init; } = string.Empty;

        /// <summary>Creates a file-level error (no row or column context).</summary>
        public static ValidationError File(string message) => new() { Message = message };

        /// <summary>Creates a header-level error.</summary>
        public static ValidationError Header(string message) => new() { Message = message };

        /// <summary>Creates a row-level error with full location context.</summary>
        public static ValidationError Row(int rowNumber, string columnName, string message) =>
            new() { RowNumber = rowNumber, ColumnName = columnName, Message = message };

        /// <summary>Creates a row-level structural error (wrong column count, etc.).</summary>
        public static ValidationError RowStructure(int rowNumber, string message) =>
            new() { RowNumber = rowNumber, Message = message };

        public override string ToString() =>
            RowNumber.HasValue && ColumnName is not null
                ? $"Row {RowNumber}, Column '{ColumnName}': {Message}"
                : RowNumber.HasValue
                    ? $"Row {RowNumber}: {Message}"
                    : Message;
    }
}
