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
                var userCount = await _db.Users.CountAsync();
                result["users_table"] = "exists";
                result["user_count"] = userCount;

                var instCount = await _db.Institutions.CountAsync();
                result["institutions_table"] = "exists";
                result["institution_count"] = instCount;
            }
        }
        catch (Exception ex)
        {
            result["database_connected"] = true; // Connection works but tables missing
            result["database_error"] = ex.Message;
        }

        result["status"] = result.ContainsKey("user_count") ? "healthy" : "degraded";
        return Ok(result);
    }

    /// <summary>
    /// Force-create database tables from the DbContext model.
    /// Use this once to bootstrap the schema, then remove.
    /// </summary>
    [HttpPost("ensure-created")]
    public async Task<IActionResult> EnsureCreated()
    {
        try
        {
            var existed = await _db.Database.EnsureCreatedAsync();
            return Ok(new
            {
                message = existed ? "Database already existed — tables verified." : "Database created successfully.",
                tables_created = !existed
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }
}
