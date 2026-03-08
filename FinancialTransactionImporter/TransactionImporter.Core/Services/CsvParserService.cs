using System.Globalization;
using Microsoft.Extensions.Options;
using TransactionImporter.Core.Entities;
using TransactionImporter.Core.Interfaces;
using TransactionImporter.Core.Models;

namespace TransactionImporter.Core.Services
{
    /// <summary>
    /// Parses and validates CSV files containing financial transactions.
    /// All rows are evaluated — every validation error is collected before
    /// the result is returned, so callers receive the full picture in one pass.
    /// </summary>
    public class CsvParserService : ICsvParserService
    {
        private readonly CsvSettings _settings;

        private static readonly string[] ExpectedHeaders =
            ["TransactionTime", "Amount", "Description", "TransactionId"];

        public CsvParserService(IOptions<CsvSettings> settings)
        {
            _settings = settings.Value;
        }

        public CsvValidationResult ParseAndValidate(Stream csvStream)
        {
            var result = new CsvValidationResult();
            var errors = new List<ValidationError>();
            var transactions = new List<Transaction>();
            var seenTransactionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // ── Distinguish a zero-byte stream from a stream with only whitespace ──
            if (csvStream.Length == 0)
            {
                errors.Add(ValidationError.File("The uploaded file is empty (0 bytes)."));
                result.Errors = errors;
                return result;
            }

            using var reader = new StreamReader(csvStream);

            var headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                errors.Add(ValidationError.File("The CSV file contains no content after the byte-order mark or leading whitespace."));
                result.Errors = errors;
                return result;
            }

            var delimiter = ResolveDelimiter(_settings.Delimiter);
            var headers = headerLine.Split(delimiter);

            var headerErrors = ValidateHeaders(headers);
            if (headerErrors.Count > 0)
            {
                // Header errors make further row parsing meaningless — return immediately.
                result.Errors = headerErrors;
                return result;
            }

            int rowNumber = 1; // Row 1 = headers; data starts at row 2
            string? line;

            while ((line = reader.ReadLine()) is not null)
            {
                rowNumber++;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var rowErrors = ValidateRow(line, delimiter, rowNumber, seenTransactionIds, out var transaction);

                if (rowErrors.Count > 0)
                    errors.AddRange(rowErrors);
                else
                    transactions.Add(transaction!);
            }

            if (errors.Count == 0 && transactions.Count == 0)
            {
                errors.Add(ValidationError.File("The CSV file contains no data rows."));
            }

            result.Errors = errors;
            result.IsValid = errors.Count == 0;

            if (result.IsValid)
                result.ParsedTransactions = transactions;

            return result;
        }

        private List<ValidationError> ValidateRow(
            string line,
            char delimiter,
            int rowNumber,
            HashSet<string> seenTransactionIds,
            out Transaction? transaction)
        {
            transaction = null;
            var errors = new List<ValidationError>();
            var columns = SplitRespectingQuotes(line, delimiter);

            if (columns.Length != ExpectedHeaders.Length)
            {
                errors.Add(ValidationError.RowStructure(rowNumber,
                    $"Expected {ExpectedHeaders.Length} columns but found {columns.Length}."));
                return errors; // Cannot validate individual fields without the right structure
            }

            // ── Column 1: TransactionTime ──────────────────────────────────────
            var rawTime = columns[0].Trim();
            DateTime transactionTime = default;

            if (string.IsNullOrWhiteSpace(rawTime))
            {
                errors.Add(ValidationError.Row(rowNumber, "TransactionTime", "Value cannot be empty."));
            }
            else if (!DateTime.TryParseExact(rawTime, _settings.DateFormat,
                         CultureInfo.InvariantCulture, DateTimeStyles.None, out transactionTime))
            {
                errors.Add(ValidationError.Row(rowNumber, "TransactionTime",
                    $"'{rawTime}' is not a valid timestamp. Expected format: '{_settings.DateFormat}'."));
            }

            // ── Column 2: Amount ───────────────────────────────────────────────
            var rawAmount = columns[1].Trim();
            decimal amount = default;

            if (string.IsNullOrWhiteSpace(rawAmount))
            {
                errors.Add(ValidationError.Row(rowNumber, "Amount", "Value cannot be empty."));
            }
            else if (!decimal.TryParse(rawAmount,
                         NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                         CultureInfo.InvariantCulture, out amount))
            {
                errors.Add(ValidationError.Row(rowNumber, "Amount",
                    $"'{rawAmount}' is not a valid numeric value."));
            }
            else if (!HasExactlyTwoDecimalPlaces(rawAmount))
            {
                errors.Add(ValidationError.Row(rowNumber, "Amount",
                    $"'{rawAmount}' must have exactly 2 decimal places (e.g. 123.45)."));
            }
            else if (_settings.MaxAbsoluteAmount > 0 && Math.Abs(amount) > _settings.MaxAbsoluteAmount)
            {
                errors.Add(ValidationError.Row(rowNumber, "Amount",
                    $"'{rawAmount}' exceeds the maximum allowed absolute value of {_settings.MaxAbsoluteAmount:F2}."));
            }

            // ── Column 3: Description ──────────────────────────────────────────
            var description = columns[2].Trim();

            if (string.IsNullOrWhiteSpace(description))
                errors.Add(ValidationError.Row(rowNumber, "Description", "Value cannot be empty."));

            // ── Column 4: TransactionId ────────────────────────────────────────
            var transactionId = columns[3].Trim();

            if (string.IsNullOrWhiteSpace(transactionId))
            {
                errors.Add(ValidationError.Row(rowNumber, "TransactionId", "Value cannot be empty."));
            }
            else if (!seenTransactionIds.Add(transactionId))
            {
                errors.Add(ValidationError.Row(rowNumber, "TransactionId",
                    $"Duplicate TransactionId '{transactionId}' — already seen earlier in this file."));
            }

            if (errors.Count > 0)
                return errors;

            transaction = new Transaction
            {
                TransactionTime = transactionTime,
                Amount = amount,
                Description = description,
                TransactionId = transactionId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return errors;
        }

        /// <summary>
        /// Resolves the configured delimiter string to its character equivalent.
        /// Supports: "," / "comma", ";" / "semicolon", "|" / "pipe", or any single character.
        /// </summary>
        public static char ResolveDelimiter(string delimiterSetting) =>
            delimiterSetting.ToLowerInvariant().Trim() switch
            {
                "," or "comma" => ',',
                ";" or "semicolon" => ';',
                "|" or "pipe" => '|',
                _ when delimiterSetting.Length == 1 => delimiterSetting[0],
                _ => ','
            };

        private static List<ValidationError> ValidateHeaders(string[] headers)
        {
            var errors = new List<ValidationError>();

            if (headers.Length != ExpectedHeaders.Length)
            {
                errors.Add(ValidationError.Header(
                    $"Invalid header count. Expected {ExpectedHeaders.Length} columns but found {headers.Length}."));
                return errors;
            }

            for (int i = 0; i < ExpectedHeaders.Length; i++)
            {
                var actual = headers[i].Trim();
                if (!string.Equals(actual, ExpectedHeaders[i], StringComparison.Ordinal))
                {
                    errors.Add(ValidationError.Header(
                        $"Invalid header at column {i + 1}. Expected '{ExpectedHeaders[i]}' but found '{actual}'."));
                }
            }

            return errors;
        }

        /// <summary>
        /// Checks that the raw string value has exactly two digits after the decimal point.
        /// </summary>
        private static bool HasExactlyTwoDecimalPlaces(string value)
        {
            var dotIndex = value.IndexOf('.');
            if (dotIndex == -1)
                return false;

            var decimalPart = value[(dotIndex + 1)..];
            return decimalPart.Length == 2 && decimalPart.All(char.IsDigit);
        }

        /// <summary>
        /// Splits a CSV line by the given delimiter while respecting double-quoted fields.
        /// </summary>
        private static string[] SplitRespectingQuotes(string line, char delimiter)
        {
            var fields = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delimiter && !inQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString());
            return [.. fields];
        }
    }
}
