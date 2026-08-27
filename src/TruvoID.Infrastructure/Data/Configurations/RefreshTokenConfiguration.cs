using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TruvoID.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.UserId).HasColumnName("user_id");
        builder.Property(t => t.Token).HasColumnName("token").HasMaxLength(128).IsRequired();
        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at");
        builder.Property(t => t.IsRevoked).HasColumnName("is_revoked");
        builder.Property(t => t.RevokedAt).HasColumnName("revoked_at");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(t => t.Token).IsUnique();
        builder.HasIndex(t => new { t.UserId, t.IsRevoked });
    }
}
