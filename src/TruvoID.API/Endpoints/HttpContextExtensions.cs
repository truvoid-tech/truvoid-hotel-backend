using Microsoft.AspNetCore.Http;

namespace TruvoID.API.Endpoints;

public static class HttpContextExtensions
{
    public static Guid GetInstitutionId(this HttpContext ctx)
    {
        var claim = ctx.User.FindFirst("institution_id")
            ?? ctx.User.FindFirst("institutionId");

        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            return Guid.Empty;

        return id;
    }

    public static Guid GetUserId(this HttpContext ctx)
    {
        var claim = ctx.User.FindFirst("sub")
            ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            return Guid.Empty;

        return id;
    }

    /// <summary>Set only when the request authenticated via an X-API-Key header rather than a JWT.</summary>
    public static Guid? GetApiKeyId(this HttpContext ctx)
    {
        var claim = ctx.User.FindFirst("api_key_id");
        return claim is not null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }
}
