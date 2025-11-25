using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AvantiPoint.Packages.Server.Extensions;

public static class JwtTokenExtensions
{
    public static string GenerateJwtToken(this HttpContext httpContext, IConfiguration configuration, ClaimsPrincipal user)
    {
        var jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key must be configured.");
        var issuer = configuration["Jwt:Issuer"] ?? "AvantiPoint.Packages";
        var audience = configuration["Jwt:Audience"] ?? "AvantiPoint.Packages";
        var expirationMinutes = int.Parse(configuration["Jwt:ExpirationMinutes"] ?? "60");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>();
        
        // Add all claims from the authenticated user
        claims.AddRange(user.Claims);
        
        // Ensure we have a name identifier
        if (!claims.Any(c => c.Type == ClaimTypes.NameIdentifier))
        {
            var nameId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? user.FindFirst("sub")?.Value 
                ?? user.Identity?.Name 
                ?? Guid.NewGuid().ToString();
            claims.Add(new Claim(ClaimTypes.NameIdentifier, nameId));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

