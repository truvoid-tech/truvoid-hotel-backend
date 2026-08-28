using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TruvoID.Infrastructure.Data;

namespace TruvoID.API.Controllers;

[ApiController]
[Route("")]
public class HealthController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly IConfiguration _config;

    public HealthController(MongoDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpGet("")]
    public IActionResult Root() => Ok(new { status = "ok", service = "TruvoID API", version = "3.7.0", database = "MongoDB" });

    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var result = new Dictionary<string, object>();

        try
        {
            // Ping MongoDB
            await _db.Users.CountDocumentsAsync(FilterDefinition<TruvoID.Domain.Entities.User>.Empty);
            result["database_connected"] = true;

            // Count collections
            var collections = new[] { "institutions", "users", "api_keys", "wallet_ledger_entries", "verification_calls", "audit_logs", "pricing_rates", "refresh_tokens" };
            result["collections"] = collections;

            var userCount = await _db.Users.CountDocumentsAsync(FilterDefinition<TruvoID.Domain.Entities.User>.Empty);
            result["user_count"] = userCount;
        }
        catch (Exception ex)
        {
            result["database_connected"] = false;
            result["error"] = ex.Message;
        }

        result["status"] = result.ContainsKey("user_count") && Convert.ToInt64(result["user_count"]) > 0 ? "healthy" : "degraded";
        return Ok(result);
    }

    [HttpPost("seed-admin")]
    public async Task<IActionResult> SeedAdmin()
    {
        try
        {
            var hasAdmin = await _db.Users.CountDocumentsAsync(u => u.Role == TruvoID.Domain.Enums.UserRole.PlatformAdmin) > 0;
            if (hasAdmin)
                return Ok(new { message = "PlatformAdmin already exists" });

            var adminUser = new TruvoID.Domain.Entities.User
            {
                Email = "admin@truvoid.ng",
                FullName = "TruvoID Platform Admin",
                PhoneNumber = "+2348000000000",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@12345"),
                Role = TruvoID.Domain.Enums.UserRole.PlatformAdmin,
                Status = TruvoID.Domain.Enums.UserStatus.Active,
                DailyCallLimit = null
            };
            await _db.Users.InsertOneAsync(adminUser);

            return Ok(new { message = "PlatformAdmin seeded", email = "admin@truvoid.ng", password = "Admin@12345" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
    }
}
