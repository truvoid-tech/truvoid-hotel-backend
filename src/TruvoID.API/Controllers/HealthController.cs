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

        // Check DB connection
        try
        {
            var canConnect = await _db.Database.CanConnectAsync();
            result["database_connected"] = canConnect;

            if (canConnect)
            {
                // Check if tables exist
                var userCount = await _db.Users.CountAsync();
                result["users_table"] = "exists";
                result["user_count"] = userCount;

                var instCount = await _db.Institutions.CountAsync();
                result["institutions_table"] = "exists";
                result["institution_count"] = instCount;

                var pricingCount = await _db.PricingRates.CountAsync();
                result["pricing_table"] = "exists";
                result["pricing_count"] = pricingCount;
            }
        }
        catch (Exception ex)
        {
            result["database_connected"] = false;
            result["database_error"] = ex.Message;
        }

        result["status"] = result.ContainsKey("database_connected") && (bool)result["database_connected"] ? "healthy" : "degraded";
        return Ok(result);
    }
}
