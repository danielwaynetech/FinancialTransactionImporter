using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using TransactionImporter.Core.Entities;
using TransactionImporter.Infrastructure.Data;
using TransactionImporter.Infrastructure.Repositories;

namespace TransactionImporter.Tests;

/// <summary>
/// Integration-style unit tests for TransactionRepository using an
/// in-memory SQLite database. Each test gets a fresh, isolated database
/// via [SetUp] so there is no shared state between tests.
/// </summary>
[TestFixture]
public class TransactionRepositoryTests
{
    private AppDbContext _context = null!;
    private TransactionRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        // A unique name per test ensures complete isolation
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new AppDbContext(options);
        _context.Database.OpenConnection();   // Keep connection open — :memory: drops on close
        _context.Database.EnsureCreated();

        _repository = new TransactionRepository(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static Transaction MakeTransaction(
        string id,
        DateTime? time = null,
        decimal amount = 10.00m,
        string description = "Test") => new()
    {
        TransactionId   = id,
        TransactionTime = time ?? DateTime.UtcNow,
        Amount          = amount,
        Description     = description,
        CreatedAt       = DateTime.UtcNow,
        UpdatedAt       = DateTime.UtcNow
    };

    private async Task SeedAsync(params Transaction[] transactions)
    {
        await _repository.AddRangeAsync(transactions);
        await _repository.SaveChangesAsync();
    }

    // ─── AddRangeAsync ────────────────────────────────────────────────────────

    [Test]
    public async Task AddRangeAsync_PersistsAllRecords()
    {
        var transactions = new[]
        {
            MakeTransaction("TXN001"),
            MakeTransaction("TXN002"),
            MakeTransaction("TXN003")
        };

        await _repository.AddRangeAsync(transactions);
        await _repository.SaveChangesAsync();

        var stored = await _context.Transactions.ToListAsync();
        stored.Should().HaveCount(3);
        stored.Select(t => t.TransactionId).Should().BeEquivalentTo("TXN001", "TXN002", "TXN003");
    }

    // ─── GetByIdAsync ─────────────────────────────────────────────────────────

    [Test]
    public async Task GetByIdAsync_ExistingId_ReturnsTransaction()
    {
        await SeedAsync(MakeTransaction("TXN001"));
        var seeded = await _context.Transactions.FirstAsync();

        var result = await _repository.GetByIdAsync(seeded.Id);

        result.Should().NotBeNull();
        result!.TransactionId.Should().Be("TXN001");
    }

    [Test]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(9999);

        result.Should().BeNull();
    }

    [Test]
    public async Task GetByIdAsync_SoftDeletedRecord_ReturnsNull()
    {
        // Seed and then soft-delete
        await SeedAsync(MakeTransaction("TXN001"));
        var seeded = await _context.Transactions.FirstAsync();

        await _repository.DeleteAsync(seeded.Id);
        await _repository.SaveChangesAsync();

        // The global query filter should exclude it from FindAsync
        var result = await _repository.GetByIdAsync(seeded.Id);
        result.Should().BeNull();
    }

    // ─── GetExistingTransactionIdsAsync ───────────────────────────────────────

    [Test]
    public async Task GetExistingTransactionIdsAsync_ReturnsOnlyMatchingIds()
    {
        await SeedAsync(
            MakeTransaction("TXN001"),
            MakeTransaction("TXN002"),
            MakeTransaction("TXN003"));

        var result = await _repository.GetExistingTransactionIdsAsync(["TXN001", "TXN003", "TXN999"]);

        result.Should().BeEquivalentTo("TXN001", "TXN003");
    }

    [Test]
    public async Task GetExistingTransactionIdsAsync_NoMatches_ReturnsEmpty()
    {
        await SeedAsync(MakeTransaction("TXN001"));

        var result = await _repository.GetExistingTransactionIdsAsync(["TXN999", "TXN888"]);

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetExistingTransactionIdsAsync_SoftDeletedRecords_AreNotReturned()
    {
        await SeedAsync(MakeTransaction("TXN001"), MakeTransaction("TXN002"));
        var toDelete = await _context.Transactions.FirstAsync(t => t.TransactionId == "TXN001");

        await _repository.DeleteAsync(toDelete.Id);
        await _repository.SaveChangesAsync();

        // TXN001 is soft-deleted — it should not appear as "existing"
        var result = await _repository.GetExistingTransactionIdsAsync(["TXN001", "TXN002"]);

        result.Should().ContainSingle()
            .Which.Should().Be("TXN002");
    }

    // ─── GetPaginatedAsync ────────────────────────────────────────────────────

    [Test]
    public async Task GetPaginatedAsync_ReturnsCorrectPage()
    {
        // Seed 5 transactions with known times for deterministic ordering
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 1; i <= 5; i++)
            await SeedAsync(MakeTransaction($"TXN00{i}", time: baseTime.AddDays(i)));

        // Page 2 with page size 2 — should return records 3 and 4 (desc order)
        var result = await _repository.GetPaginatedAsync(pageNumber: 2, pageSize: 2);

        result.TotalCount.Should().Be(5);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.TotalPages.Should().Be(3);
        result.Items.Should().HaveCount(2);
    }

    [Test]
    public async Task GetPaginatedAsync_OrdersByTransactionTimeDescending()
    {
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await SeedAsync(
            MakeTransaction("TXN_EARLIEST", time: baseTime.AddDays(1)),
            MakeTransaction("TXN_MIDDLE",   time: baseTime.AddDays(2)),
            MakeTransaction("TXN_LATEST",   time: baseTime.AddDays(3)));

        var result = await _repository.GetPaginatedAsync(1, 10);

        result.Items.Select(t => t.TransactionId)
            .Should().ContainInOrder("TXN_LATEST", "TXN_MIDDLE", "TXN_EARLIEST");
    }

    [Test]
    public async Task GetPaginatedAsync_TotalCountExcludesSoftDeletedRecords()
    {
        await SeedAsync(
            MakeTransaction("TXN001"),
            MakeTransaction("TXN002"),
            MakeTransaction("TXN003"));

        var toDelete = await _context.Transactions.FirstAsync(t => t.TransactionId == "TXN001");
        await _repository.DeleteAsync(toDelete.Id);
        await _repository.SaveChangesAsync();

        var result = await _repository.GetPaginatedAsync(1, 10);

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().NotContain(t => t.TransactionId == "TXN001");
    }

    [Test]
    public async Task GetPaginatedAsync_EmptyDatabase_ReturnsTotalCountOfZero()
    {
        var result = await _repository.GetPaginatedAsync(1, 20);

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
        result.TotalPages.Should().Be(0);
    }

    // ─── UpdateAsync ─────────────────────────────────────────────────────────

    [Test]
    public async Task UpdateAsync_PersistsChangedFields()
    {
        await SeedAsync(MakeTransaction("TXN001", amount: 10.00m, description: "Original"));
        var transaction = await _context.Transactions.FirstAsync();

        transaction.Amount      = 99.99m;
        transaction.Description = "Updated";
        transaction.UpdatedAt   = DateTime.UtcNow;

        await _repository.UpdateAsync(transaction);
        await _repository.SaveChangesAsync();

        // Re-query to confirm persistence
        var updated = await _context.Transactions.FirstAsync(t => t.TransactionId == "TXN001");
        updated.Amount.Should().Be(99.99m);
        updated.Description.Should().Be("Updated");
    }

    // ─── DeleteAsync (Soft Delete) ────────────────────────────────────────────

    [Test]
    public async Task DeleteAsync_SetsIsDeletedTrue()
    {
        await SeedAsync(MakeTransaction("TXN001"));
        var transaction = await _context.Transactions.FirstAsync();
        var id = transaction.Id;

        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();

        // Bypass the global query filter to inspect the raw record
        var raw = await _context.Transactions
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Id == id);

        raw.IsDeleted.Should().BeTrue();
        raw.DeletedAt.Should().NotBeNull();
        raw.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task DeleteAsync_RecordIsNotPhysicallyRemoved()
    {
        await SeedAsync(MakeTransaction("TXN001"));
        var transaction = await _context.Transactions.FirstAsync();
        var id = transaction.Id;

        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();

        // Record must still exist in the raw table
        var rawCount = await _context.Transactions
            .IgnoreQueryFilters()
            .CountAsync(t => t.Id == id);

        rawCount.Should().Be(1);
    }

    [Test]
    public async Task DeleteAsync_SoftDeletedRecord_IsExcludedFromStandardQueries()
    {
        await SeedAsync(MakeTransaction("TXN001"), MakeTransaction("TXN002"));
        var toDelete = await _context.Transactions.FirstAsync(t => t.TransactionId == "TXN001");

        await _repository.DeleteAsync(toDelete.Id);
        await _repository.SaveChangesAsync();

        var visible = await _context.Transactions.ToListAsync();
        visible.Should().ContainSingle()
            .Which.TransactionId.Should().Be("TXN002");
    }

    [Test]
    public async Task DeleteAsync_NonExistentId_DoesNotThrow()
    {
        var act = async () =>
        {
            await _repository.DeleteAsync(9999);
            await _repository.SaveChangesAsync();
        };

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task DeleteAsync_AlreadySoftDeletedRecord_RemainsDeleted()
    {
        await SeedAsync(MakeTransaction("TXN001"));
        var transaction = await _context.Transactions.FirstAsync();
        var id = transaction.Id;

        // Delete once
        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();

        // The record is now filtered out — DeleteAsync on a missing record is a no-op
        var act = async () =>
        {
            await _repository.DeleteAsync(id);
            await _repository.SaveChangesAsync();
        };

        await act.Should().NotThrowAsync();

        var raw = await _context.Transactions
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Id == id);

        raw.IsDeleted.Should().BeTrue();
    }
}
