using Microsoft.EntityFrameworkCore;
using TransactionImporter.Core.Entities;

namespace TransactionImporter.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.TransactionId)
                  .IsUnique();

            entity.Property(e => e.TransactionId)
                  .IsRequired()
                  .HasMaxLength(255);

            entity.Property(e => e.Description)
                  .IsRequired()
                  .HasMaxLength(1000);

            entity.Property(e => e.Amount)
                  .HasColumnType("TEXT");

            entity.Property(e => e.TransactionTime)
                  .IsRequired();

            entity.Property(e => e.CreatedAt)
                  .IsRequired();

            entity.Property(e => e.UpdatedAt)
                  .IsRequired();

            entity.Property(e => e.IsDeleted)
                  .IsRequired()
                  .HasDefaultValue(false);

            entity.Property(e => e.DeletedAt)
                  .IsRequired(false);

            // Global query filter — soft-deleted records are invisible to all
            // standard queries throughout the application. Use IgnoreQueryFilters()
            // explicitly if you ever need to query deleted records (e.g. audit).
            entity.HasQueryFilter(e => !e.IsDeleted);
        });
    }
}
