using System.IO;
using System.Security.Cryptography.X509Certificates;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Services;
using AvantiPoint.Packages.Host.Admin.Services.Secrets;
using AvantiPoint.Packages.Host.Admin.Services.Tokens;
using AvantiPoint.Packages.Host.Admin.Services.Upstreams;
using AvantiPoint.Packages.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AvantiPoint.Packages.Host.Admin.Extensions;

public static class HostAdminServiceExtensions
{
    public static IServiceCollection AddHostAdminServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HostSettings>(configuration.GetSection("Host"));
        services.Configure<HostAccessOptions>(configuration.GetSection("Host:Access"));
        services.Configure<HostAuthenticationOptions>(configuration.GetSection("Host:Authentication"));

        services.AddHttpContextAccessor();

        // Encrypt stored feed credentials (upstream source secrets, downstream publish tokens)
        // at rest. Registered before AddNuGetPackageApi so Core's fallback NullSecretProtector
        // is never used in the Host.
        //
        // The key ring MUST be persisted to a durable location shared across restarts/instances,
        // or previously-encrypted secrets become unreadable the moment the key ring is lost (for
        // example a container recreated with only /data mounted). Configure
        // Host:DataProtection:KeyPath to point at the same durable volume as the database/storage;
        // appsettings.Docker.json sets this to /data/dataprotection-keys, alongside /data/packages.db.
        var dataProtection = services.AddDataProtection()
            .SetApplicationName(configuration["Host:DataProtection:ApplicationName"] ?? "AvantiPoint.Packages.Host");

        var keyPath = configuration["Host:DataProtection:KeyPath"];
        if (!string.IsNullOrWhiteSpace(keyPath))
        {
            Directory.CreateDirectory(keyPath);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        }

        var certificatePath = configuration["Host:DataProtection:CertificatePath"];
        if (!string.IsNullOrWhiteSpace(certificatePath))
        {
            // macOS imports temporary keychain entries and does not support EphemeralKeySet.
            var keyStorage = OperatingSystem.IsMacOS() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet;
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath,
                configuration["Host:DataProtection:CertificatePassword"], keyStorage);
            using var publicKey = certificate.GetRSAPublicKey();
            if (!certificate.HasPrivateKey || publicKey is null)
                throw new InvalidOperationException("The Data Protection certificate must contain an RSA private key.");
            dataProtection.ProtectKeysWithCertificate(certificate);
            var previousCertificates = configuration.GetSection("Host:DataProtection:PreviousCertificates").GetChildren()
                .Select(section => X509CertificateLoader.LoadPkcs12FromFile(section["Path"]
                    ?? throw new InvalidOperationException("A previous Data Protection certificate requires Path."),
                    section["Password"], keyStorage));
            dataProtection.UnprotectKeysWithAnyCertificate([certificate, .. previousCertificates]);
        }

        services.TryAddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        services.AddSingleton<IHostTokenHasher, HostTokenHasher>();
        services.AddScoped<HostApiTokenService>();
        services.AddScoped<IPackageAuthenticationService, DatabasePackageAuthenticationService>();
        services.AddScoped<IHostUserProvisioner, HostUserProvisioner>();
        services.AddScoped<HostUserManagementService>();
        services.AddScoped<HostPublishTargetSourceImporter>();
        services.AddScoped<IHostAuditLogService, HostAuditLogService>();
        services.AddScoped<ConfigurationPackageSourceSeeder>();
        services.AddHttpClient(PackageSourceConnectionTester.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AvantiPoint.Packages.Host/source-check");
        });
        services.AddScoped<IPackageSourceConnectionTester, PackageSourceConnectionTester>();
        services.AddHttpClient(nameof(HostExternalLoginValidator));
        services.AddHttpClient(HostMicrosoftGroupResolver.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.MaxResponseContentBufferSize = 32 * 1024;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<HostMicrosoftGroupResolver>();
        services.AddScoped<IHostExternalLoginValidator, HostExternalLoginValidator>();
        services.AddScoped<INuGetFeedActionHandler, HostNuGetFeedActionHandler>();
        services.AddScoped<ISyndicationService, SyndicationService>();
        services.AddScoped<IDownstreamPublishService, DownstreamPublishService>();
        services.AddHttpClient(nameof(Services.Publishers.NpmDownstreamPublisher));
        services.AddHttpClient(nameof(Services.Publishers.OciDownstreamPublisher));
        services.AddScoped<Services.Publishers.IDownstreamPublisher, Services.Publishers.NuGetDownstreamPublisher>();
        services.AddScoped<Services.Publishers.IDownstreamPublisher, Services.Publishers.NpmDownstreamPublisher>();
        services.AddScoped<Services.Publishers.IDownstreamPublisher, Services.Publishers.OciDownstreamPublisher>();

        services.AddOptions<Services.Events.HostWebhookOptions>().Bind(configuration.GetSection("Host:Webhooks"))
            .Validate(options => options.Subscriptions.Count <= 32, "At most 32 webhook subscriptions are supported per host.")
            .Validate(options => options.Subscriptions.Select(s => s.StorageId).Distinct().Count() == options.Subscriptions.Count,
                "Webhook subscriptions must have distinct identities (set Id when URLs are shared).")
            .Validate(options => options.Subscriptions.All(s => s.StartAfterEventId >= 0
                && Uri.TryCreate(s.Url, UriKind.Absolute, out var url) && url.Scheme is "https" or "http" && string.IsNullOrEmpty(url.UserInfo)),
                "Webhooks require an HTTP(S) URL without embedded credentials and a non-negative StartAfterEventId.")
            .ValidateOnStart();
        services.AddScoped<Services.Events.HostWebhookQueue>();
        services.AddScoped<Services.Events.IHostEventService, Services.Events.HostEventService>();
        services.AddScoped<AvantiPoint.Feed.Platform.Callbacks.IProtocolNeutralFeedActionHandler, Services.Events.AuditEventFeedActionHandler>();
        services.AddScoped<AvantiPoint.Feed.Platform.Callbacks.IProtocolNeutralFeedActionHandler, Services.SyndicationFeedActionHandler>();
        services.AddHttpClient(nameof(Services.Events.WebhookDispatcherService))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHostedService<Services.Events.WebhookDispatcherService>();

        services.AddHostEmailServices(configuration);
        services.AddHostedService<TokenExpirationNotificationService>();

        return services;
    }
}
