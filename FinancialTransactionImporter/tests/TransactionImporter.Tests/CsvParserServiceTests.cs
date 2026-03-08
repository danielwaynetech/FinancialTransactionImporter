using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using TransactionImporter.Core.Models;
using TransactionImporter.Core.Services;

namespace TransactionImporter.Tests
{
    [TestFixture]
    public class CsvParserServiceTests
    {
        // ─── Helpers ──────────────────────────────────────────────────────────────

        private static CsvParserService CreateService(
            string delimiter = ",",
            string dateFormat = "yyyy-MM-dd HH:mm:ss",
            decimal maxAbsoluteAmount = 9_999_999.99m)
        {
            var options = Options.Create(new CsvSettings
            {
                Delimiter = delimiter,
                DateFormat = dateFormat,
                MaxAbsoluteAmount = maxAbsoluteAmount
            });
            return new CsvParserService(options);
        }

        private static Stream ToStream(string content)
            => new MemoryStream(Encoding.UTF8.GetBytes(content));

        private static Stream EmptyStream()
        {
            var ms = new MemoryStream();
            ms.Position = 0;
            return ms;
        }

        // ─── Happy Path ───────────────────────────────────────────────────────────

        [Test]
        public void ParseAndValidate_ValidCsv_ReturnsSuccess()
        {
            const string csv = "TransactionTime,Amount,Description,TransactionId\n" +
                               "2024-01-15 10:30:00,123.45,Grocery Store,TXN001\n" +
                               "2024-01-16 14:00:00,67.89,Online Purchase,TXN002";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.ParsedTransactions.Should().HaveCount(2);
            result.ParsedTransactions[0].TransactionId.Should().Be("TXN001");
            result.ParsedTransactions[0].Amount.Should().Be(123.45m);
            result.ParsedTransactions[0].Description.Should().Be("Grocery Store");
        }

        [Test]
        public void ParseAndValidate_ValidCsv_ParsesDateCorrectly()
        {
            const string csv = "TransactionTime,Amount,Description,TransactionId\n" +
                               "2024-06-30 23:59:59,10.00,Test,TXN001";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeTrue();
            var dt = result.ParsedTransactions[0].TransactionTime;
            dt.Year.Should().Be(2024);
            dt.Month.Should().Be(6);
            dt.Day.Should().Be(30);
            dt.Hour.Should().Be(23);
            dt.Minute.Should().Be(59);
            dt.Second.Should().Be(59);
        }

        [Test]
        public void ParseAndValidate_MinimumOneRow_ReturnsSuccess()
        {
            const string csv = "TransactionTime,Amount,Description,TransactionId\n" +
                               "2024-01-01 00:00:00,0.01,Single entry,TXN001";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeTrue();
            result.ParsedTransactions.Should().ContainSingle();
        }

        // ─── Empty / Blank File ───────────────────────────────────────────────────

        [Test]
        public void ParseAndValidate_ZeroBytesStream_ReturnsSpecificEmptyFileError()
        {
            var result = CreateService().ParseAndValidate(EmptyStream());

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle()
                .Which.Message.Should().Contain("0 bytes");
        }

        [Test]
        public void ParseAndValidate_OnlyWhitespace_ReturnsDifferentErrorToZeroBytes()
        {
            var result = CreateService().ParseAndValidate(ToStream("   \n  \n  "));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle()
                .Which.Message.Should().NotContain("0 bytes");
        }

        [Test]
        public void ParseAndValidate_HeaderOnlyNoDataRows_ReturnsError()
        {
            const string csv = "TransactionTime,Amount,Description,TransactionId";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle()
                .Which.Message.ToLower().Should().Contain("no data rows");
        }

        // ─── All-Errors Collection (not fail-fast) ────────────────────────────────

        [Test]
        public void ParseAndValidate_MultipleInvalidRows_ReturnsAllErrors()
        {
            const string csv = "TransactionTime,Amount,Description,TransactionId\n" +
                               "BADDATE,10.00,Row2,TXN001\n" +
                               "2024-01-15 10:30:00,BADAMOUNT,Row3,TXN002\n" +
                               "2024-01-16 10:30:00,20.00,,TXN003\n" +
                               "2024-01-17 10:30:00,30.00,Row5,TXN004";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().HaveCount(3);
            result.ParsedTransactions.Should().BeEmpty();
            result.Errors.Should().Contain(e => e.RowNumber == 2);
            result.Errors.Should().Contain(e => e.RowNumber == 3);
            result.Errors.Should().Contain(e => e.RowNumber == 4);
        }

        [Test]
        public void ParseAndValidate_RowWithMultipleErrors_ReportsEachColumnSeparately()
        {
            const string csv = "TransactionTime,Amount,Description,TransactionId\n" +
                               "BADDATE,BADAMOUNT,,TXN001";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().HaveCount(3);
            result.Errors.Should().Contain(e => e.ColumnName == "TransactionTime");
            result.Errors.Should().Contain(e => e.ColumnName == "Amount");
            result.Errors.Should().Contain(e => e.ColumnName == "Description");
        }

        // ─── Header Validation ────────────────────────────────────────────────────

        [Test]
        public void ParseAndValidate_WrongHeaderName_ReturnsDescriptiveError()
        {
            const string csv = "Date,Amount,Description,TransactionId\n" +
                               "2024-01-01 00:00:00,10.00,Test,TXN001";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle()
                .Which.Message.Should().Contain("TransactionTime").And.Contain("Date");
        }

        [Test]
        public void ParseAndValidate_AllHeadersWrong_ReportsEachHeaderSeparately()
        {
            const string csv = "col1,col2,col3,col4\n2024-01-01 00:00:00,10.00,Test,TXN001";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().HaveCount(4);
        }

        [Test]
        public void ParseAndValidate_TooFewHeaders_ReturnsCountError()
        {
            const string csv = "TransactionTime,Amount,Description\n2024-01-01 00:00:00,10.00,Test";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle();
        }

        [Test]
        public void ParseAndValidate_HeadersCaseSensitive_WrongCase_ReturnsError()
        {
            const string csv = "transactiontime,Amount,Description,TransactionId\n" +
                               "2024-01-01 00:00:00,10.00,Test,TXN001";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
        }

        // ─── Structured Error Fields ──────────────────────────────────────────────

        [Test]
        public void ParseAndValidate_InvalidRow_ErrorHasCorrectRowNumberAndColumnName()
        {
            const string csv = "TransactionTime,Amount,Description,TransactionId\n" +
                               "2024-01-15 10:30:00,NOTANUMBER,Test,TXN001";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
            var error = result.Errors.Should().ContainSingle().Subject;
            error.RowNumber.Should().Be(2);
            error.ColumnName.Should().Be("Amount");
            error.Message.Should().Contain("NOTANUMBER");
        }

        [Test]
        public void ValidationError_RowToString_IncludesRowAndColumn()
        {
            var error = ValidationError.Row(5, "Amount", "Some message");
            error.ToString().Should().Be("Row 5, Column 'Amount': Some message");
        }

        [Test]
        public void ValidationError_FileToString_HasNoRowOrColumn()
        {
            var error = ValidationError.File("File is empty.");
            error.ToString().Should().Be("File is empty.");
        }

        // ─── TransactionTime Validation ───────────────────────────────────────────

        [TestCase("01/15/2024 10:30:00", TestName = "WrongDateFormat")]
        [TestCase("2024-01-15", TestName = "MissingTimeComponent")]
        [TestCase("not-a-date", TestName = "GarbageString")]
        [TestCase("", TestName = "EmptyDate")]
        [TestCase("2024-13-01 00:00:00", TestName = "InvalidMonth")]
        [TestCase("2024-01-32 00:00:00", TestName = "InvalidDay")]
        public void ParseAndValidate_InvalidTransactionTime_ErrorHasRowAndColumn(string badDate)
        {
            var csv = $"TransactionTime,Amount,Description,TransactionId\n{badDate},10.00,Test,TXN001";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.RowNumber == 2 && e.ColumnName == "TransactionTime");
        }

        [Test]
        public void ParseAndValidate_CustomDateFormat_ParsesCorrectly()
        {
            const string csv = "TransactionTime,Amount,Description,TransactionId\n" +
                               "15/01/2024,10.00,Test,TXN001";

            var result = CreateService(dateFormat: "dd/MM/yyyy").ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeTrue();
            result.ParsedTransactions[0].TransactionTime.Should().Be(new DateTime(2024, 1, 15));
        }

        [Test]
        public void ParseAndValidate_CustomDateFormat_WrongInput_ErrorIncludesFormatHint()
        {
            const string csv = "TransactionTime,Amount,Description,TransactionId\n" +
                               "2024-01-15 10:30:00,10.00,Test,TXN001";

            var result = CreateService(dateFormat: "dd/MM/yyyy").ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.ColumnName == "TransactionTime" && e.Message.Contains("dd/MM/yyyy"));
        }

        // ─── Amount Validation ────────────────────────────────────────────────────

        [TestCase("abc", TestName = "NotANumber")]
        [TestCase("", TestName = "EmptyAmount")]
        [TestCase("1.2.3", TestName = "MultipleDecimalPoints")]
        [TestCase("$10.00", TestName = "CurrencySymbol")]
        public void ParseAndValidate_InvalidAmountNotNumeric_ErrorNamesAmountColumn(string badAmount)
        {
            var csv = $"TransactionTime,Amount,Description,TransactionId\n2024-01-15 10:30:00,{badAmount},Test,TXN001";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ColumnName == "Amount");
        }

        [TestCase("100", TestName = "NoDecimalPlaces")]
        [TestCase("100.1", TestName = "OneDecimalPlace")]
        [TestCase("100.123", TestName = "ThreeDecimalPlaces")]
        [TestCase("100.0", TestName = "OneTrailingZero")]
        public void ParseAndValidate_AmountWithoutExactlyTwoDecimals_ReturnsError(string badAmount)
        {
            var csv = $"TransactionTime,Amount,Description,TransactionId\n2024-01-15 10:30:00,{badAmount},Test,TXN001";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.ColumnName == "Amount" && e.Message.ToLower().Contains("2 decimal"));
        }

        [TestCase("0.00", TestName = "Zero")]
        [TestCase("123.45", TestName = "PositiveAmount")]
        [TestCase("-50.00", TestName = "NegativeAmount")]
        [TestCase("9999999.99", TestName = "AtMaxBound")]
        public void ParseAndValidate_ValidAmounts_Accepted(string validAmount)
        {
            var csv = $"TransactionTime,Amount,Description,TransactionId\n2024-01-15 10:30:00,{validAmount},Test,TXN001";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void ParseAndValidate_AmountExceedsMaxAbsoluteAmount_ReturnsError()
        {
            const string csv = "TransactionTime,Amount,Description,TransactionId\n" +
                               "2024-01-15 10:30:00,10000000.00,Test,TXN001";

            var result = CreateService(maxAbsoluteAmount: 9_999_999.99m).ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.ColumnName == "Amount" && e.Message.Contains("maximum"));
        }

        [Test]
        public void ParseAndValidate_NegativeAmountExceedsMaxAbsoluteAmount_ReturnsError()
        {
            const string csv = "TransactionTime,Amount,Description,TransactionId\n" +
                               "2024-01-15 10:30:00,-10000000.00,Test,TXN001";

            var result = CreateService(maxAbsoluteAmount: 9_999_999.99m).ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ColumnName == "Amount");
        }

        [Test]
        public void ParseAndValidate_MaxAbsoluteAmountZero_DisablesBoundsCheck()
        {
            const string csv = "TransactionTime,Amount,Description,TransactionId\n" +
                               "2024-01-15 10:30:00,99999999999.99,Test,TXN001";

            var result = CreateService(maxAbsoluteAmount: 0m).ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeTrue();
        }

        // ─── Description Validation ───────────────────────────────────────────────

        [TestCase("", TestName = "EmptyDescription")]
        [TestCase("   ", TestName = "WhitespaceDescription")]
        public void ParseAndValidate_EmptyOrWhitespaceDescription_ErrorNamesDescriptionColumn(string badDesc)
        {
            var csv = $"TransactionTime,Amount,Description,TransactionId\n2024-01-15 10:30:00,10.00,{badDesc},TXN001";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ColumnName == "Description");
        }

        // ─── TransactionId Validation ─────────────────────────────────────────────

        [Test]
        public void ParseAndValidate_EmptyTransactionId_ErrorNamesTransactionIdColumn()
        {
            const string csv = "TransactionTime,Amount,Description,TransactionId\n" +
                               "2024-01-15 10:30:00,10.00,Test,";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ColumnName == "TransactionId");
        }

        [Test]
        public void ParseAndValidate_DuplicateTransactionIdWithinFile_ErrorIncludesRowNumber()
        {
            const string csv = "TransactionTime,Amount,Description,TransactionId\n" +
                               "2024-01-15 10:30:00,10.00,First,TXN001\n" +
                               "2024-01-16 10:30:00,20.00,Second,TXN001";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
            var error = result.Errors.Should().ContainSingle(e => e.ColumnName == "TransactionId").Subject;
            error.RowNumber.Should().Be(3);
            error.Message.ToLower().Should().Contain("duplicate");
            error.Message.Should().Contain("TXN001");
        }

        // ─── Row Structure ────────────────────────────────────────────────────────

        [Test]
        public void ParseAndValidate_RowWithTooFewColumns_ErrorHasRowNumber()
        {
            const string csv = "TransactionTime,Amount,Description,TransactionId\n" +
                               "2024-01-15 10:30:00,10.00,Test";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.RowNumber == 2);
        }

        // ─── Delimiter Configuration ──────────────────────────────────────────────

        [Test]
        public void ParseAndValidate_SemicolonDelimiter_ParsesCorrectly()
        {
            const string csv = "TransactionTime;Amount;Description;TransactionId\n" +
                               "2024-01-15 10:30:00;50.00;Test Purchase;TXN001";

            var result = CreateService(delimiter: ";").ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeTrue();
            result.ParsedTransactions[0].Amount.Should().Be(50.00m);
        }

        [Test]
        public void ParseAndValidate_PipeDelimiter_ParsesCorrectly()
        {
            const string csv = "TransactionTime|Amount|Description|TransactionId\n" +
                               "2024-01-15 10:30:00|75.50|Pipe test|TXN001";

            var result = CreateService(delimiter: "|").ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeTrue();
            result.ParsedTransactions[0].Amount.Should().Be(75.50m);
        }

        // ─── ResolveDelimiter ─────────────────────────────────────────────────────

        [TestCase(",", ',', TestName = "CommaChar")]
        [TestCase("comma", ',', TestName = "CommaWord")]
        [TestCase(";", ';', TestName = "SemicolonChar")]
        [TestCase("semicolon", ';', TestName = "SemicolonWord")]
        [TestCase("|", '|', TestName = "PipeChar")]
        [TestCase("pipe", '|', TestName = "PipeWord")]
        [TestCase("COMMA", ',', TestName = "CommaUpperCase")]
        [TestCase("PIPE", '|', TestName = "PipeUpperCase")]
        public void ResolveDelimiter_KnownValues_ReturnsCorrectChar(string input, char expected)
        {
            CsvParserService.ResolveDelimiter(input).Should().Be(expected);
        }

        // ─── Blank Lines & Quoted Fields ─────────────────────────────────────────

        [Test]
        public void ParseAndValidate_BlankLinesBetweenRows_SkipsAndSucceeds()
        {
            const string csv = "TransactionTime,Amount,Description,TransactionId\r\n" +
                               "2024-01-15 10:30:00,10.00,Test,TXN001\r\n" +
                               "\r\n" +
                               "2024-01-16 10:30:00,20.00,Test2,TXN002\r\n";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeTrue();
            result.ParsedTransactions.Should().HaveCount(2);
        }

        [Test]
        public void ParseAndValidate_QuotedDescriptionWithComma_ParsesCorrectly()
        {
            const string csv = "TransactionTime,Amount,Description,TransactionId\n" +
                               "2024-01-15 10:30:00,10.00,\"Coffee, snack & tip\",TXN001";

            var result = CreateService().ParseAndValidate(ToStream(csv));

            result.IsValid.Should().BeTrue();
            result.ParsedTransactions[0].Description.Should().Be("Coffee, snack & tip");
        }
    }
}
