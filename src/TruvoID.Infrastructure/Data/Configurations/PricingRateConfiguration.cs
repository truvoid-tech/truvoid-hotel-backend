using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruvoID.Domain.Entities;

namespace TruvoID.Infrastructure.Data.Configurations;

public class PricingRateConfiguration : IEntityTypeConfiguration<PricingRate>
{
    public void Configure(EntityTypeBuilder<PricingRate> builder)
    {
        builder.ToTable("pricing_rates");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.InstitutionId).HasColumnName("institution_id");
        builder.Property(r => r.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(50);
        builder.Property(r => r.PricePerCall).HasColumnName("price_per_call").HasPrecision(18, 2);
        builder.Property(r => r.NimcPartnerCost).HasColumnName("nimc_partner_cost").HasPrecision(18, 2);
        builder.Property(r => r.IsActive).HasColumnName("is_active");
        builder.Property(r => r.EffectiveFrom).HasColumnName("effective_from");
        builder.Property(r => r.EffectiveTo).HasColumnName("effective_to");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(r => r.Institution)
            .WithMany(i => i.PricingRates)
            .HasForeignKey(r => r.InstitutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.Type, r.InstitutionId, r.IsActive });
    }
}
