using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruvoID.Domain.Entities;

namespace TruvoID.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");

        builder.Property(u => u.InstitutionId).HasColumnName("institution_id");
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(256);
        builder.Property(u => u.PhoneNumber).HasColumnName("phone_number").HasMaxLength(50);
        builder.Property(u => u.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(50);
        builder.Property(u => u.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50);
        builder.Property(u => u.DailyCallLimit).HasColumnName("daily_call_limit");
        builder.Property(u => u.LastLoginAt).HasColumnName("last_login_at");
        builder.Property(u => u.PasswordResetToken).HasColumnName("password_reset_token").HasMaxLength(256);
        builder.Property(u => u.PasswordResetTokenExpiry).HasColumnName("password_reset_token_expiry");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(u => u.Institution)
            .WithMany(i => i.Users)
            .HasForeignKey(u => u.InstitutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(u => new { u.InstitutionId, u.Email }).IsUnique();
        builder.HasIndex(u => u.Status);
    }
}
