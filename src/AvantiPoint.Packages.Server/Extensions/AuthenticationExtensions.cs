using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using AvantiPoint.Packages.Server.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AvantiPoint.Packages.Server.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddMicrosoftAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        MicrosoftAuthOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            throw new InvalidOperationException("Microsoft authentication requires ClientId and ClientSecret to be configured.");
        }

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = "Cookies";
            options.DefaultChallengeScheme = MicrosoftAccountDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = "Cookies";
        })
        .AddCookie("Cookies")
        .AddMicrosoftAccount(microsoftOptions =>
        {
            microsoftOptions.ClientId = options.ClientId;
            microsoftOptions.ClientSecret = options.ClientSecret;
            
            // Force tenant-specific authentication
            if (!string.IsNullOrWhiteSpace(options.TenantId) && options.TenantId != "common")
            {
                microsoftOptions.AuthorizationEndpoint = $"https://login.microsoftonline.com/{options.TenantId}/oauth2/v2.0/authorize";
                microsoftOptions.TokenEndpoint = $"https://login.microsoftonline.com/{options.TenantId}/oauth2/v2.0/token";
            }
        })
        .AddJwtBearer(jwtOptions =>
        {
            var jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key must be configured.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = configuration["Jwt:Issuer"] ?? "AvantiPoint.Packages",
                ValidateAudience = true,
                ValidAudience = configuration["Jwt:Audience"] ?? "AvantiPoint.Packages",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

        return services;
    }

    public static IServiceCollection AddGoogleAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        GoogleAuthOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            throw new InvalidOperationException("Google authentication requires ClientId and ClientSecret to be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.HostedDomain))
        {
            throw new InvalidOperationException("Google authentication requires HostedDomain to be configured for per-tenant authentication.");
        }

        services.AddAuthentication(authOptions =>
        {
            authOptions.DefaultScheme = "Cookies";
            authOptions.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
            authOptions.DefaultSignInScheme = "Cookies";
        })
        .AddCookie("Cookies")
        .AddGoogle(googleOptions =>
        {
            googleOptions.ClientId = options.ClientId;
            googleOptions.ClientSecret = options.ClientSecret;
            
            // Force workspace domain authentication by adding hd parameter
            if (!string.IsNullOrWhiteSpace(options.HostedDomain))
            {
                googleOptions.Events.OnRedirectToAuthorizationEndpoint = context =>
                {
                    var uriBuilder = new UriBuilder(context.RedirectUri);
                    var query = QueryHelpers.ParseQuery(uriBuilder.Query);
                    var queryDict = query.ToDictionary(k => k.Key, v => v.Value.ToString());
                    queryDict["hd"] = options.HostedDomain;
                    uriBuilder.Query = QueryHelpers.AddQueryString("", queryDict).TrimStart('?');
                    context.RedirectUri = uriBuilder.ToString();
                    return Task.CompletedTask;
                };
            }
        })
        .AddJwtBearer(jwtOptions =>
        {
            var jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key must be configured.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = configuration["Jwt:Issuer"] ?? "AvantiPoint.Packages",
                ValidateAudience = true,
                ValidAudience = configuration["Jwt:Audience"] ?? "AvantiPoint.Packages",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

        return services;
    }
}

