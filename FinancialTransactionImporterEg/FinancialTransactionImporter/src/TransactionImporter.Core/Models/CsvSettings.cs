namespace TransactionImporter.Core.Models;

public class CsvSettings
{
    public const string SectionName = "CsvSettings";

    /// <summary>
    /// Supported values: "," or "comma", ";" or "semicolon", "|" or "pipe"
    /// </summary>
    public string Delimiter { get; set; } = ",";

    /// <summary>
    /// Expected date/time format string. E.g. "yyyy-MM-dd HH:mm:ss"
    /// </summary>
    public string DateFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// Maximum absolute value allowed for the Amount column.
    /// Amounts whose absolute value exceeds this are rejected.
    /// Set to 0 to disable the check.
    /// </summary>
    public decimal MaxAbsoluteAmount { get; set; } = 9_999_999.99m;
}
