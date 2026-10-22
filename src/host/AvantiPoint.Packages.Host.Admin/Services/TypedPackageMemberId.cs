using AvantiPoint.Packages.Host.Admin.Entities;

namespace AvantiPoint.Packages.Host.Admin.Services;

public readonly record struct TypedPackageMemberId(
    PublishTargetProtocol Protocol,
    string ArtifactName,
    string OriginalId)
{
    public static TypedPackageMemberId Parse(string packageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        var trimmed = packageId.Trim();
        return ParseCore(trimmed);
    }

    private static TypedPackageMemberId ParseCore(string trimmed)
    {
        if (TryPrefix(trimmed, "nuget:", out var rest))
        {
            return new(PublishTargetProtocol.NuGet, rest, trimmed);
        }

        if (TryPrefix(trimmed, "npm:", out rest))
        {
            return new(PublishTargetProtocol.Npm, rest, trimmed);
        }

        if (TryPrefix(trimmed, "oci:", out rest))
        {
            return new(PublishTargetProtocol.Oci, rest, trimmed);
        }

        return Infer(trimmed);
    }

    private static TypedPackageMemberId Infer(string trimmed)
    {
        if (trimmed.StartsWith("@"))
        {
            return new(PublishTargetProtocol.Npm, trimmed, trimmed);
        }

        if (trimmed.Contains("/"))
        {
            return new(PublishTargetProtocol.Oci, trimmed, trimmed);
        }

        return new(PublishTargetProtocol.NuGet, trimmed, trimmed);
    }

    public static string SkipReason(PublishTargetProtocol memberProtocol, PublishTargetProtocol targetProtocol) =>
        string.Format("{0} member cannot be pushed to a {1} target", ProtocolLabel(memberProtocol), ProtocolLabel(targetProtocol));

    private static string ProtocolLabel(PublishTargetProtocol protocol)
    {
        return protocol.ToString();
    }

    private static bool TryPrefix(string value, string prefix, out string rest)
    {
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            rest = value[prefix.Length..].Trim();
            return rest.Length > 0;
        }

        rest = value;
        return false;
    }
}
