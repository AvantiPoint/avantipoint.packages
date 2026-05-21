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

public class PackageActionContext
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? IPAddress { get; set; }
    public string? TokenDescription { get; set; }
    public string? UserAgent { get; set; }
}
