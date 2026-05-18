using AvantiPoint.Packages.Server.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Mvc;

namespace AvantiPoint.Packages.Server.Controllers;

[Route("auth")]
public class AuthCallbackController : Controller
{
    private readonly IConfiguration _configuration;

    public AuthCallbackController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("signin-microsoft")]
    public async Task<IActionResult> MicrosoftSignIn()
    {
        var result = await HttpContext.AuthenticateAsync(MicrosoftAccountDefaults.AuthenticationScheme);
        if (result.Succeeded && result.Principal != null)
        {
            // Already authenticated, generate JWT and sign in
            var token = HttpContext.GenerateJwtToken(_configuration, result.Principal);
            var claims = result.Principal.Claims.ToList();
            var jwtIdentity = new System.Security.Claims.ClaimsIdentity(claims, "Cookies");
            var jwtPrincipal = new System.Security.Claims.ClaimsPrincipal(jwtIdentity);
            await HttpContext.SignInAsync("Cookies", jwtPrincipal);
            return Redirect("/");
        }
        
        // Not authenticated, challenge
        return Challenge(new AuthenticationProperties { RedirectUri = "/auth/signin-microsoft" }, MicrosoftAccountDefaults.AuthenticationScheme);
    }

    [HttpGet("signin-google")]
    public async Task<IActionResult> GoogleSignIn()
    {
        var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
        if (result.Succeeded && result.Principal != null)
        {
            // Already authenticated, generate JWT and sign in
            var token = HttpContext.GenerateJwtToken(_configuration, result.Principal);
            var claims = result.Principal.Claims.ToList();
            var jwtIdentity = new System.Security.Claims.ClaimsIdentity(claims, "Cookies");
            var jwtPrincipal = new System.Security.Claims.ClaimsPrincipal(jwtIdentity);
            await HttpContext.SignInAsync("Cookies", jwtPrincipal);
            return Redirect("/");
        }
        
        // Not authenticated, challenge
        return Challenge(new AuthenticationProperties { RedirectUri = "/auth/signin-google" }, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("signout")]
    public new async Task<IActionResult> SignOut()
    {
        await HttpContext.SignOutAsync();
        return Redirect("/");
    }
}
