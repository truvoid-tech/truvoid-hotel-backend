using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruvoID.Domain.Entities;

namespace TruvoID.Infrastructure.Data.Configurations;

public class InstitutionConfiguration : IEntityTypeConfiguration<Institution>
{
    public void Configure(EntityTypeBuilder<Institution> builder)
    {
        builder.ToTable("institutions");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");

        builder.Property(i => i.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        builder.Property(i => i.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(50);
        builder.Property(i => i.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50);
        builder.Property(i => i.ContactEmail).HasColumnName("contact_email").HasMaxLength(256);
        builder.Property(i => i.ContactPhone).HasColumnName("contact_phone").HasMaxLength(50);
        builder.Property(i => i.Address).HasColumnName("address").HasMaxLength(512);
        builder.Property(i => i.CacRcNumber).HasColumnName("cac_rc_number").HasMaxLength(50);
        builder.Property(i => i.CacCertificateUrl).HasColumnName("cac_certificate_url").HasMaxLength(512);
        builder.Property(i => i.ExpectedMonthlyVolume).HasColumnName("expected_monthly_volume").HasMaxLength(50);
        builder.Property(i => i.PrimaryUseCase).HasColumnName("primary_use_case").HasMaxLength(50);
        builder.Property(i => i.ComplianceAccepted).HasColumnName("compliance_accepted");
        builder.Property(i => i.ComplianceAcceptedAt).HasColumnName("compliance_accepted_at");
        builder.Property(i => i.ResellerAcknowledged).HasColumnName("reseller_acknowledged");
        builder.Property(i => i.DataProcessingAgreed).HasColumnName("data_processing_agreed");
        builder.Property(i => i.OnboardingStep).HasColumnName("onboarding_step");
        builder.Property(i => i.OnboardingCompleted).HasColumnName("onboarding_completed");
        builder.Property(i => i.BrandingMetadataJson).HasColumnName("branding_metadata_json");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(i => i.Name);
        builder.HasIndex(i => i.Status);
    }
}
