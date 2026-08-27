using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruvoID.Domain.Entities;

namespace TruvoID.Infrastructure.Data.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");

        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).HasColumnName("id");

        builder.Property(k => k.InstitutionId).HasColumnName("institution_id");
        builder.Property(k => k.KeyHash).HasColumnName("key_hash").HasMaxLength(512).IsRequired();
        builder.Property(k => k.KeyPrefix).HasColumnName("key_prefix").HasMaxLength(16).IsRequired();
        builder.Property(k => k.Description).HasColumnName("description").HasMaxLength(256);
        builder.Property(k => k.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50);
        builder.Property(k => k.RevokedAt).HasColumnName("revoked_at");
        builder.Property(k => k.RateLimitPerMinute).HasColumnName("rate_limit_per_minute");
        builder.Property(k => k.RateLimitPerDay).HasColumnName("rate_limit_per_day");
        builder.Property(k => k.CreatedAt).HasColumnName("created_at");
        builder.Property(k => k.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(k => k.Institution)
            .WithMany(i => i.ApiKeys)
            .HasForeignKey(k => k.InstitutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(k => k.KeyHash).IsUnique();
        builder.HasIndex(k => k.Status);
    }
}
