using TransactionImporter.Core.Models;

namespace TransactionImporter.Core.Interfaces
{
    public interface ICsvParserService
    {
        /// <summary>
        /// Parses and validates a CSV stream, returning a result containing
        /// the parsed transactions or a list of validation errors.
        /// </summary>
        CsvValidationResult ParseAndValidate(Stream csvStream);
    }
}
