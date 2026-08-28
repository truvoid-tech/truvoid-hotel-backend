using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruvoID.Infrastructure.Data;

namespace TruvoID.API.Controllers;

[ApiController]
[Route("")]
public class HealthController : ControllerBase
{
    private readonly TruvoIDDbContext _db;

    public HealthController(TruvoIDDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public IActionResult Root() => Ok(new { status = "ok", service = "TruvoID API", version = "2.5.0" });

    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var result = new Dictionary<string, object>();

        try
        {
            var canConnect = await _db.Database.CanConnectAsync();
            result["database_connected"] = canConnect;

            if (canConnect)
            {
                var tables = await ListTablesAsync();
                result["tables"] = tables;

                if (tables.Contains("users"))
                {
                    var userCount = await _db.Users.CountAsync();
                    result["user_count"] = userCount;
                }
            }
        }
        catch (Exception ex)
        {
            result["error"] = ex.Message;
        }

        result["status"] = result.ContainsKey("user_count") && (long)result["user_count"] > 0 ? "healthy" : "degraded";
        return Ok(result);
    }

    /// <summary>
    /// Drop all tables and recreate from DbContext model, then seed.
    /// Use only once to fix schema mismatches.
    /// </summary>
    [HttpPost("rebuild-schema")]
    public async Task<IActionResult> RebuildSchema()
    {
        try
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();

            // Drop all tables in public schema
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    DO $$ DECLARE
                        r RECORD;
                    BEGIN
                        FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public') LOOP
                            EXECUTE 'DROP TABLE IF EXISTS ' || quote_ident(r.tablename) || ' CASCADE';
                        END LOOP;
                    END $$;";
                await cmd.ExecuteNonQueryAsync();
            }

            // Create all tables from DbContext model
            await _db.Database.EnsureCreatedAsync();

            var tables = await ListTablesAsync();

            // Seed
            await SeedData.SeedAsync(_db);

            var userCount = await _db.Users.CountAsync();

            return Ok(new
            {
                message = "Schema rebuilt and seeded successfully",
                tables = tables,
                user_count = userCount
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
    }

    [HttpPost("seed-admin")]
    public async Task<IActionResult> SeedAdmin()
    {
        try
        {
            await SeedData.SeedAsync(_db);
            var userCount = await _db.Users.CountAsync();
            return Ok(new { message = "Seed completed", user_count = userCount });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
    }

    private async Task<List<string>> ListTablesAsync()
    {
        var conn = _db.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT tablename FROM pg_tables WHERE schemaname = 'public'";
        using var reader = await cmd.ExecuteReaderAsync();
        var tables = new List<string>();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));
        return tables;
    }
}
