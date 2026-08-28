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
    public IActionResult Root() => Ok(new { status = "ok", service = "TruvoID API", version = "2.4.2" });

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

        result["status"] = result.ContainsKey("user_count") ? "healthy" : "degraded";
        return Ok(result);
    }

    /// <summary>
    /// Drop migration history and recreate all tables from the DbContext model.
    /// </summary>
    [HttpPost("ensure-created")]
    public async Task<IActionResult> EnsureCreated()
    {
        try
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();

            // Drop __EFMigrationsHistory if it exists (it blocks EnsureCreated from creating tables)
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DROP TABLE IF EXISTS \"__EFMigrationsHistory\" CASCADE";
                await cmd.ExecuteNonQueryAsync();
            }

            // Now EnsureCreated will create everything fresh
            var existed = await _db.Database.EnsureCreatedAsync();

            var tables = await ListTablesAsync();

            return Ok(new
            {
                message = tables.Contains("users") ? "All tables created successfully!" : "Tables may not have been created — check logs.",
                database_existed = existed,
                tables_found = tables,
                table_count = tables.Count
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
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
