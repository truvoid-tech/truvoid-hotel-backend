using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruvoID.Domain.Entities;

namespace TruvoID.Infrastructure.Data.Configurations;

public class WalletLedgerEntryConfiguration : IEntityTypeConfiguration<WalletLedgerEntry>
{
    public void Configure(EntityTypeBuilder<WalletLedgerEntry> builder)
    {
        builder.ToTable("wallet_ledger_entries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.InstitutionId).HasColumnName("institution_id");
        builder.Property(e => e.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(18, 2);
        builder.Property(e => e.BalanceAfter).HasColumnName("balance_after").HasPrecision(18, 2);
        builder.Property(e => e.ReferenceId).HasColumnName("reference_id").HasMaxLength(256);
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(512);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.Institution)
            .WithMany(i => i.WalletLedgerEntries)
            .HasForeignKey(e => e.InstitutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.InstitutionId, e.CreatedAt });
        builder.HasIndex(e => e.ReferenceId);
    }
}
