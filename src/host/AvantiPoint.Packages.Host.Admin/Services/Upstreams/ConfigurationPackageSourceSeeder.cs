using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Host.Admin.Services.Upstreams;

/// <summary>
/// Reconciles package sources declared through configuration into the runtime database.
/// </summary>
public sealed class ConfigurationPackageSourceSeeder(
    IConfiguration configuration,
    IContext context,
    IPackageSourceService packageSourceService,
    ISecretProtector secretProtector,
    ILogger<ConfigurationPackageSourceSeeder> logger)
{
    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        var configuredSources = configuration
            .GetSection("PackageSources")
            .Get<List<PackageSourceConfiguration>>() ?? [];

        if (configuredSources.Count == 0)
        {
            return;
        }

        Validate(configuredSources);

        var existingSources = await context.PackageSources
            .AsNoTracking()
            .OrderBy(source => source.Id)
            .ToListAsync(cancellationToken);

        foreach (var configured in configuredSources)
        {
            var existing = existingSources.FirstOrDefault(source =>
                source.Protocol == configured.Protocol
                && string.Equals(source.Name, configured.Name.Trim(), StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                var created = ToEntity(configured);
                await packageSourceService.AddAsync(created, cancellationToken);
                existingSources.Add(created);

                logger.LogInformation(
                    "Created {Protocol} package source {SourceName} from configuration",
                    created.Protocol,
                    created.Name);
                continue;
            }

            if (!ApplyConfiguration(existing, configured))
            {
                continue;
            }

            await packageSourceService.UpdateAsync(existing, cancellationToken);
            logger.LogInformation(
                "Updated {Protocol} package source {SourceName} from configuration",
                existing.Protocol,
                existing.Name);
        }
    }

    private bool ApplyConfiguration(PackageSource target, PackageSourceConfiguration configured)
    {
        var surface = configured.Protocol == PackageSourceProtocol.Oci
            ? NullIfWhiteSpace(configured.Surface)
            : null;
        var feedUrl = configured.FeedUrl.Trim();
        var name = configured.Name.Trim();

        var changed =
            !string.Equals(target.Name, name, StringComparison.Ordinal)
            || !string.Equals(target.FeedUrl, feedUrl, StringComparison.Ordinal)
            || target.Type != configured.Type
            || target.Protocol != configured.Protocol
            || target.Priority != configured.Priority
            || !string.Equals(target.Surface, surface, StringComparison.Ordinal)
            || target.CachingStrategy != configured.CachingStrategy
            || target.MirrorSignaturePolicy != configured.MirrorSignaturePolicy
            || target.IsEnabled != configured.IsEnabled;

        target.Name = name;
        target.FeedUrl = feedUrl;
        target.Type = configured.Type;
        target.Protocol = configured.Protocol;
        target.Priority = configured.Priority;
        target.Surface = surface;
        target.CachingStrategy = configured.CachingStrategy;
        target.MirrorSignaturePolicy = configured.MirrorSignaturePolicy;
        target.IsEnabled = configured.IsEnabled;

        changed |= ApplySecret(configured.Username, target.Username, value => target.Username = value);
        changed |= ApplySecret(configured.Password, target.Password, value => target.Password = value);
        changed |= ApplySecret(configured.ApiKey, target.ApiKey, value => target.ApiKey = value);

        return changed;
    }

    private bool ApplySecret(string? configuredValue, string? storedValue, Action<string?> assign)
    {
        // An omitted value leaves an existing secret untouched. Supplying an empty string is an
        // explicit request to clear it, which is important for environment-driven deployments.
        if (configuredValue is null)
        {
            assign(storedValue);
            return false;
        }

        if (string.Equals(secretProtector.Unprotect(storedValue), configuredValue, StringComparison.Ordinal))
        {
            assign(storedValue);
            return false;
        }

        assign(configuredValue);
        return true;
    }

    private static PackageSource ToEntity(PackageSourceConfiguration configured) => new()
    {
        Name = configured.Name.Trim(),
        FeedUrl = configured.FeedUrl.Trim(),
        Type = configured.Type,
        Protocol = configured.Protocol,
        Priority = configured.Priority,
        Surface = configured.Protocol == PackageSourceProtocol.Oci
            ? NullIfWhiteSpace(configured.Surface)
            : null,
        CachingStrategy = configured.CachingStrategy,
        MirrorSignaturePolicy = configured.MirrorSignaturePolicy,
        IsEnabled = configured.IsEnabled,
        Username = configured.Username,
        Password = configured.Password,
        ApiKey = configured.ApiKey,
    };

    private static void Validate(IReadOnlyCollection<PackageSourceConfiguration> sources)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source.Name))
            {
                throw new InvalidOperationException("Every PackageSources entry must have a non-empty Name.");
            }

            if (!Uri.TryCreate(source.FeedUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    $"Package source '{source.Name}' must have an absolute HTTP or HTTPS FeedUrl.");
            }

            if (source.Priority < 0)
            {
                throw new InvalidOperationException(
                    $"Package source '{source.Name}' cannot have a negative Priority.");
            }

            var key = $"{source.Protocol}:{source.Name.Trim()}";
            if (!seen.Add(key))
            {
                throw new InvalidOperationException(
                    $"PackageSources contains more than one {source.Protocol} source named '{source.Name.Trim()}'.");
            }
        }
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
