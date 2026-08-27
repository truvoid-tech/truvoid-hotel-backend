using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TruvoID.Infrastructure.Data;

/// <summary>
/// Design-time factory for EF Core CLI tooling (dotnet ef migrations).
/// Uses environment variables or appsettings for the connection string.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TruvoIDDbContext>
{
    public TruvoIDDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TruvoIDDbContext>();

        // Try environment variable first, fall back to a default dev connection string
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=truvoid;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);

        return new TruvoIDDbContext(optionsBuilder.Options);
    }
}
