using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Services.Email;
using AvantiPoint.Packages.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Host.Admin.Services;

public sealed class HostNuGetFeedActionHandler(
    IHttpContextAccessor contextAccessor,
    IEmailService emailService,
    IContext context,
    ISyndicationService syndicationService,
    Events.IHostEventService eventService,
    ILogger<HostNuGetFeedActionHandler> logger) : INuGetFeedActionHandler
{
    private HttpContext? HttpContext => contextAccessor.HttpContext;
    private ClaimsPrincipal User => HttpContext?.User ?? new ClaimsPrincipal();
    private System.Net.IPAddress? RemoteIp => HttpContext?.Connection.RemoteIpAddress;
    private string? UserAgent => HttpContext?.Request.Headers.UserAgent.ToString();

    public Task<bool> CanDownloadPackage(string packageId, string version) =>
        Task.FromResult(User.IsInRole(FeedRoles.Consumer));

    public async Task OnPackageDownloaded(string packageId, string version)
    {
        try
        {
            logger.LogInformation("{User} downloaded {Package} {Version}", User.Identity?.Name, packageId, version);
            if (RemoteIp is not null &&
                await context.PackageDownloads.CountAsync(x => x.RemoteIp != null && x.RemoteIp.Equals(RemoteIp)) == 1)
            {
                await SendEmailAsync(EmailTemplateNames.TokenFirstUse, "Token used from new IP Address", packageId, version);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in OnPackageDownloaded for {Package} {Version}", packageId, version);
        }
    }

    public async Task OnPackageUploaded(string packageId, string version)
    {
        logger.LogInformation("{User} uploaded {Package} {Version}", User.Identity?.Name, packageId, version);
        await eventService.RecordAsync("package.published", packageId, $"version={version}");
        await SendEmailAsync(EmailTemplateNames.PackageUploaded, $"Package Uploaded - {packageId} {version}", packageId, version);
        await syndicationService.SyndicatePackageAsync(packageId, NuGetVersion.Parse(version));
    }

    public Task OnSymbolsDownloaded(string packageId, string version) => Task.CompletedTask;

    public async Task OnSymbolsUploaded(string packageId, string version)
    {
        logger.LogInformation("{User} uploaded symbols {Package} {Version}", User.Identity?.Name, packageId, version);
        await SendEmailAsync(EmailTemplateNames.SymbolsUploaded, $"Symbols Uploaded - {packageId} {version}", packageId, version);
        await syndicationService.SyndicateSymbolsAsync(packageId, NuGetVersion.Parse(version));
    }

    private async Task SendEmailAsync(string templateId, string subject, string packageId, string version)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var name = User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(email))
        {
            return;
        }

        var mailContext = new PackageActionContext
        {
            Id = packageId,
            Version = version,
            IPAddress = RemoteIp?.ToString(),
            TokenDescription = User.FindFirstValue(FeedClaims.TokenDescription),
            UserAgent = UserAgent,
        };

        await emailService.SendEmail(templateId, new MailAddress(email, name), subject, mailContext);
    }
}

