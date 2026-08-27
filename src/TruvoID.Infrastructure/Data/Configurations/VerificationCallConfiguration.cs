using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruvoID.Domain.Entities;

namespace TruvoID.Infrastructure.Data.Configurations;

public class VerificationCallConfiguration : IEntityTypeConfiguration<VerificationCall>
{
    public void Configure(EntityTypeBuilder<VerificationCall> builder)
    {
        builder.ToTable("verification_calls");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.InstitutionId).HasColumnName("institution_id");
        builder.Property(c => c.UserId).HasColumnName("user_id");
        builder.Property(c => c.ApiKeyId).HasColumnName("api_key_id");
        builder.Property(c => c.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.SubjectRef).HasColumnName("subject_ref").HasMaxLength(512).IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.LedgerEntryId).HasColumnName("ledger_entry_id");
        builder.Property(c => c.AmountCharged).HasColumnName("amount_charged").HasPrecision(18, 2);
        builder.Property(c => c.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(256);
        builder.Property(c => c.MatchedFieldsJson).HasColumnName("matched_fields_json");
        builder.Property(c => c.ErrorMessage).HasColumnName("error_message");
        builder.Property(c => c.UpstreamReferenceId).HasColumnName("upstream_reference_id").HasMaxLength(256);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(c => c.Institution)
            .WithMany(i => i.VerificationCalls)
            .HasForeignKey(c => c.InstitutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.User)
            .WithMany(u => u.VerificationCalls)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.ApiKey)
            .WithMany(k => k.VerificationCalls)
            .HasForeignKey(c => c.ApiKeyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.LedgerEntry)
            .WithMany()
            .HasForeignKey(c => c.LedgerEntryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => c.IdempotencyKey).IsUnique().HasFilter(null); // Only unique when not null
        builder.HasIndex(c => new { c.InstitutionId, c.CreatedAt });
        builder.HasIndex(c => c.Status);
    }
}
