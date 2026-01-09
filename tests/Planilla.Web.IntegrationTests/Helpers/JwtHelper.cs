using System.IdentityModel.Tokens.Jwt;

namespace Planilla.Web.IntegrationTests.Helpers;

public static class JwtHelper
{
    public static JwtSecurityToken ReadToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.ReadJwtToken(token);
    }

    public static string? GetClaim(string token, string claimType)
    {
        var jwtToken = ReadToken(token);
        return jwtToken.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
    }

    public static int GetTenantId(string token)
    {
        var tenantIdStr = GetClaim(token, "tenant_id");
        return int.TryParse(tenantIdStr, out var tenantId) ? tenantId : 0;
    }

    public static string? GetTenantRole(string token)
    {
        return GetClaim(token, "tenant_role");
    }

    public static string? GetPlan(string token)
    {
        return GetClaim(token, "plan");
    }
}
