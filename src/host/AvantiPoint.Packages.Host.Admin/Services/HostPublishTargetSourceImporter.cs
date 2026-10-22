using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Admin.Services;

public sealed record PublishTargetImportResult(bool Succeeded, string Message, string? TargetName = null);

/// <summary>Explicitly copies source connection settings into the outbound publishing model.</summary>
public sealed class HostPublishTargetSourceImporter(IContext packages, IHostIdentityContext identity, ISecretProtector secrets)
{
    public async Task<PublishTargetImportResult> ImportAsync(int sourceId, string actor, CancellationToken cancellationToken = default)
    {
        var source = await packages.PackageSources.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sourceId, cancellationToken);
        if (source is null) return new(false, "The source no longer exists.");
        var protocol = source.Protocol switch
        {
            PackageSourceProtocol.NuGet => PublishTargetProtocol.NuGet,
            PackageSourceProtocol.Npm => PublishTargetProtocol.Npm,
            PackageSourceProtocol.Oci => PublishTargetProtocol.Oci,
            _ => throw new InvalidOperationException("This source protocol does not support publishing."),
        };
        var endpoint = source.FeedUrl.TrimEnd('/');
        if (protocol == PublishTargetProtocol.Oci && endpoint.EndsWith("/v2", StringComparison.OrdinalIgnoreCase))
            endpoint = endpoint[..^3];
        var existing = await identity.HostPublishTargets.AsNoTracking().SingleOrDefaultAsync(t => t.Name == source.Name, cancellationToken);
        if (existing is not null)
        {
            return existing.Protocol == protocol && existing.PublishEndpoint.TrimEnd('/') == endpoint
                ? new(true, "This publish target already exists. Its credentials were preserved.", existing.Name)
                : new(false, "A different publish target already uses this name. Create a target with a unique name on the Publish Targets page.");
        }
        var credential = secrets.Unprotect(source.ApiKey);
        if (string.IsNullOrWhiteSpace(credential) && protocol == PublishTargetProtocol.Oci)
            credential = secrets.Unprotect(source.Password);
        if (string.IsNullOrWhiteSpace(credential))
            return new(false, "This source has no publishing credential. Add a target and its publish token on the Publish Targets page.");

        identity.HostPublishTargets.Add(new HostPublishTarget
        {
            Name = source.Name, PublishEndpoint = endpoint, Protocol = protocol,
            Username = protocol == PublishTargetProtocol.Oci ? secrets.Unprotect(source.Username) : null,
            ApiToken = secrets.Protect(credential)!, AddedBy = actor, Timestamp = DateTimeOffset.UtcNow,
        });
        identity.HostAuditEvents.Add(new HostAuditEvent
        {
            Actor = actor, EventType = "publish-target.created", Subject = source.Name,
            Detail = $"Imported connection from source {sourceId}; protocol={protocol}", Timestamp = DateTimeOffset.UtcNow,
        });
        await identity.SaveChangesAsync(cancellationToken);
        return new(true, "Created the publish target. Review its credentials and associate it with a package group when ready to publish.", source.Name);
    }
}
