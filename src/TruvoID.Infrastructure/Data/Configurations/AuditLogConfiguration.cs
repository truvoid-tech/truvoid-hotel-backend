using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruvoID.Domain.Entities;

namespace TruvoID.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.ActorId).HasColumnName("actor_id");
        builder.Property(a => a.ActorType).HasColumnName("actor_type").HasMaxLength(50);
        builder.Property(a => a.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Entity).HasColumnName("entity").HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasColumnName("entity_id");
        builder.Property(a => a.DetailsJson).HasColumnName("details_json");
        builder.Property(a => a.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
        builder.Property(a => a.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");

        // Audit logs are immutable — no UpdatedAt
        builder.Ignore(a => a.UpdatedAt);

        builder.HasIndex(a => a.ActorId);
        builder.HasIndex(a => new { a.Entity, a.EntityId });
        builder.HasIndex(a => a.CreatedAt);
    }
}
