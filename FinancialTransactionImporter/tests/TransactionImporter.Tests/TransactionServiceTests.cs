using FluentAssertions;
using Moq;
using NUnit.Framework;
using TransactionImporter.Core.Entities;
using TransactionImporter.Core.Interfaces;
using TransactionImporter.Core.Models;
using TransactionImporter.Core.Services;

namespace TransactionImporter.Tests
{
    [TestFixture]
    public class TransactionServiceTests
    {
        private Mock<ITransactionRepository> _repoMock = null!;
        private Mock<ICsvParserService> _parserMock = null!;
        private TransactionService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _repoMock = new Mock<ITransactionRepository>();
            _parserMock = new Mock<ICsvParserService>();
            _service = new TransactionService(_repoMock.Object, _parserMock.Object);
        }

        // ─── ImportAsync ─────────────────────────────────────────────────────────

        [Test]
        public async Task ImportAsync_ValidCsvNoDuplicates_SavesAndReturnsSuccess()
        {
            var transactions = new List<Transaction>
        {
            new() { TransactionId = "TXN001", Amount = 10.00m, Description = "Test", TransactionTime = DateTime.UtcNow }
        };

            _parserMock
                .Setup(p => p.ParseAndValidate(It.IsAny<Stream>()))
                .Returns(new CsvValidationResult { IsValid = true, ParsedTransactions = transactions });

            _repoMock
                .Setup(r => r.GetExistingTransactionIdsAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(Enumerable.Empty<string>());

            var (success, errors) = await _service.ImportAsync(Stream.Null);

            success.Should().BeTrue();
            errors.Should().BeEmpty();
            _repoMock.Verify(r => r.AddRangeAsync(transactions), Times.Once);
            _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Test]
        public async Task ImportAsync_ParserReturnsErrors_ReturnsAllErrorsWithoutSaving()
        {
            var csvErrors = new List<ValidationError>
        {
            ValidationError.Row(2, "Amount", "Not a number."),
            ValidationError.Row(5, "TransactionTime", "Bad date.")
        };

            _parserMock
                .Setup(p => p.ParseAndValidate(It.IsAny<Stream>()))
                .Returns(new CsvValidationResult { IsValid = false, Errors = csvErrors });

            var (success, errors) = await _service.ImportAsync(Stream.Null);

            success.Should().BeFalse();
            errors.Should().HaveCount(2);
            errors.Should().Contain(e => e.ColumnName == "Amount");
            errors.Should().Contain(e => e.ColumnName == "TransactionTime");
            _repoMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Transaction>>()), Times.Never);
            _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [Test]
        public async Task ImportAsync_DuplicateIdInDatabase_ErrorIncludesRowNumber()
        {
            var transactions = new List<Transaction>
        {
            new() { TransactionId = "TXN001", Amount = 10.00m, Description = "Test", TransactionTime = DateTime.UtcNow }
        };

            _parserMock
                .Setup(p => p.ParseAndValidate(It.IsAny<Stream>()))
                .Returns(new CsvValidationResult { IsValid = true, ParsedTransactions = transactions });

            _repoMock
                .Setup(r => r.GetExistingTransactionIdsAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(["TXN001"]);

            var (success, errors) = await _service.ImportAsync(Stream.Null);

            success.Should().BeFalse();
            var error = errors.Should().ContainSingle().Subject;
            error.RowNumber.Should().Be(2);
            error.ColumnName.Should().Be("TransactionId");
            error.Message.Should().Contain("TXN001").And.Contain("already exists in the database");
            _repoMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Transaction>>()), Times.Never);
        }

        [Test]
        public async Task ImportAsync_MultipleDuplicateIdsInDatabase_ReportsAllOfThem()
        {
            var transactions = new List<Transaction>
        {
            new() { TransactionId = "TXN001", TransactionTime = DateTime.UtcNow, Amount = 10.00m, Description = "A" },
            new() { TransactionId = "TXN002", TransactionTime = DateTime.UtcNow, Amount = 20.00m, Description = "B" },
            new() { TransactionId = "TXN003", TransactionTime = DateTime.UtcNow, Amount = 30.00m, Description = "C" }
        };

            _parserMock
                .Setup(p => p.ParseAndValidate(It.IsAny<Stream>()))
                .Returns(new CsvValidationResult { IsValid = true, ParsedTransactions = transactions });

            _repoMock
                .Setup(r => r.GetExistingTransactionIdsAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(["TXN001", "TXN003"]);

            var (success, errors) = await _service.ImportAsync(Stream.Null);

            success.Should().BeFalse();
            errors.Should().HaveCount(2);
            errors.Should().Contain(e => e.Message.Contains("TXN001"));
            errors.Should().Contain(e => e.Message.Contains("TXN003"));
        }

        // ─── UpdateTransactionAsync ───────────────────────────────────────────────

        [Test]
        public async Task UpdateTransactionAsync_ExistingTransaction_UpdatesFieldsAndReturnsSuccess()
        {
            var existing = new Transaction
            {
                Id = 1,
                TransactionId = "TXN001",
                Amount = 10.00m,
                Description = "Old",
                TransactionTime = DateTime.UtcNow
            };

            _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existing);

            var request = new UpdateTransactionRequest
            {
                TransactionTime = new DateTime(2024, 6, 1),
                Amount = 99.99m,
                Description = "Updated"
            };

            var (success, error, reason) = await _service.UpdateTransactionAsync(1, request);

            success.Should().BeTrue();
            error.Should().BeNull();
            reason.Should().Be(NotFoundReason.None);
            existing.Amount.Should().Be(99.99m);
            existing.Description.Should().Be("Updated");
            _repoMock.Verify(r => r.UpdateAsync(existing), Times.Once);
            _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Test]
        public async Task UpdateTransactionAsync_NonExistentId_ReturnsNeverExistedReason()
        {
            _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Transaction?)null);

            var (success, error, reason) = await _service.UpdateTransactionAsync(999, new UpdateTransactionRequest
            {
                TransactionTime = DateTime.UtcNow,
                Amount = 10.00m,
                Description = "Test"
            });

            success.Should().BeFalse();
            reason.Should().Be(NotFoundReason.NeverExisted);
            error.Should().Contain("999");
            _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Transaction>()), Times.Never);
        }

        // ─── DeleteTransactionAsync ───────────────────────────────────────────────

        [Test]
        public async Task DeleteTransactionAsync_ExistingTransaction_DeletesAndReturnsSuccess()
        {
            var existing = new Transaction { Id = 5, TransactionId = "TXN005" };
            _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(existing);

            var (success, error, reason) = await _service.DeleteTransactionAsync(5);

            success.Should().BeTrue();
            error.Should().BeNull();
            reason.Should().Be(NotFoundReason.None);
            _repoMock.Verify(r => r.DeleteAsync(5), Times.Once);
            _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Test]
        public async Task DeleteTransactionAsync_NonExistentId_ReturnsNeverExistedReason()
        {
            _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Transaction?)null);

            var (success, error, reason) = await _service.DeleteTransactionAsync(42);

            success.Should().BeFalse();
            reason.Should().Be(NotFoundReason.NeverExisted);
            error.Should().Contain("42");
            _repoMock.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        // ─── GetTransactionsAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetTransactionsAsync_PageSizeAboveMax_ClampsTo200()
        {
            _repoMock
                .Setup(r => r.GetPaginatedAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new PaginatedResult<Transaction>());

            await _service.GetTransactionsAsync(1, 9999);

            _repoMock.Verify(r => r.GetPaginatedAsync(1, 200), Times.Once);
        }

        [Test]
        public async Task GetTransactionsAsync_PageNumberBelowOne_NormalisesToOne()
        {
            _repoMock
                .Setup(r => r.GetPaginatedAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new PaginatedResult<Transaction>());

            await _service.GetTransactionsAsync(-5, 20);

            _repoMock.Verify(r => r.GetPaginatedAsync(1, 20), Times.Once);
        }
    }
}
