using Microsoft.AspNetCore.Mvc;

namespace TruvoID.API.Controllers;

[ApiController]
[Route("debug")]
public class DebugController : ControllerBase
{
    [HttpGet("env")]
    public IActionResult GetEnv()
    {
        var envVars = Environment.GetEnvironmentVariables();
        var idaccess = new Dictionary<string, string?>();
        foreach (var key in envVars.Keys)
        {
            var keyStr = key.ToString()!;
            if (keyStr.Contains("IDACCESS", StringComparison.OrdinalIgnoreCase) ||
                keyStr.Contains("NIMC", StringComparison.OrdinalIgnoreCase) ||
                keyStr.Contains("API", StringComparison.OrdinalIgnoreCase))
            {
                var val = envVars[key]?.ToString() ?? "";
                // Mask the value - show first 4 and last 4 chars
                var masked = val.Length > 8
                    ? val[..4] + new string('*', val.Length - 8) + val[^4..]
                    : val.Length > 0
                        ? new string('*', val.Length)
                        : "(empty)";
                idaccess[keyStr] = masked;
            }
        }
        return Ok(idaccess);
    }
}
