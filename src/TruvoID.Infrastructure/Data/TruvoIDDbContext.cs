using Microsoft.EntityFrameworkCore;
using TruvoID.Domain.Entities;

namespace TruvoID.Infrastructure.Data;

/// <summary>
/// Main database context for TruvoID. Uses PostgreSQL via Npgsql.
/// </summary>
public class TruvoIDDbContext : DbContext
{
    public TruvoIDDbContext(DbContextOptions<TruvoIDDbContext> options) : base(options) { }

    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<WalletLedgerEntry> WalletLedgerEntries => Set<WalletLedgerEntry>();
    public DbSet<VerificationCall> VerificationCalls => Set<VerificationCall>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PricingRate> PricingRates => Set<PricingRate>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TruvoIDDbContext).Assembly);
    }
}
