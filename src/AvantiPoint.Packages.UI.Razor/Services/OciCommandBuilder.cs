using AvantiPoint.Packages.Core.Entities.Oci;

namespace AvantiPoint.Packages.UI.Services;

public static class OciCommandBuilder
{
    public static string BuildMaskedTokenPrompt() =>
        "printf 'Registry token: '; read -rs REGISTRY_TOKEN; printf '\\n'";

    public static string BuildLogin(string client, string host, string username, bool plainHttp = false) =>
        $"printf '%s' \"$REGISTRY_TOKEN\" | {client} {host} --username \"{username}\" --password-stdin"
        + (plainHttp && client is "helm registry login" or "oras login" ? " --plain-http" : string.Empty);

    public static string BuildDockerPull(string host, string repository, string reference) =>
        $"docker pull {BuildReference(host, repository, reference)}";

    public static string BuildHelmPull(
        string host,
        string repository,
        string reference,
        bool plainHttp = false) =>
        $"helm pull {BuildHelmReference(host, repository, reference)}{BuildHelmVersionArgument(reference)}{BuildHelmRegistryOption(plainHttp)}";

    public static string BuildHelmInstall(
        string host,
        string repository,
        string reference,
        bool plainHttp = false)
    {
        var release = repository.Split('/').Last();
        return $"helm upgrade --install {release} {BuildHelmReference(host, repository, reference)}{BuildHelmVersionArgument(reference)}{BuildHelmRegistryOption(plainHttp)}";
    }

    public static string BuildOrasPull(
        string host,
        string repository,
        string reference,
        bool plainHttp = false) =>
        $"oras pull{BuildOrasRegistryOption(plainHttp)} {BuildReference(host, repository, reference)}";

    public static string BuildCranePull(
        string host,
        string repository,
        string reference,
        bool plainHttp = false) =>
        $"crane pull{BuildCraneRegistryOption(plainHttp)} {BuildReference(host, repository, reference)} image.tar";

    public static string BuildCosignVerify(
        string host,
        string repository,
        string digest,
        bool allowHttpRegistry = false) =>
        $"cosign verify --key /path/to/trusted-cosign.pub{BuildCosignRegistryOption(allowHttpRegistry)} {BuildReference(host, repository, digest)}";

    public static string BuildCosignVerifyAttestation(
        string host,
        string repository,
        string digest,
        bool allowHttpRegistry = false) =>
        $"cosign verify-attestation --key /path/to/trusted-cosign.pub{BuildCosignRegistryOption(allowHttpRegistry)} {BuildReference(host, repository, digest)}";

    public static string BuildHelmVerifiedPull(
        string host,
        string repository,
        string digest,
        bool plainHttp = false) =>
        $"helm pull {BuildHelmReference(host, repository, digest)} --verify{BuildHelmRegistryOption(plainHttp)}";

    public static IEnumerable<string> BuildArtifactCommands(
        string host,
        string repository,
        string reference,
        OciArtifactKind artifactKind,
        bool plainHttp = false)
    {
        if (artifactKind == OciArtifactKind.Helm)
        {
            yield return BuildHelmPull(host, repository, reference, plainHttp);
            yield return BuildHelmInstall(host, repository, reference, plainHttp);
            yield return BuildOrasPull(host, repository, reference, plainHttp);
            yield break;
        }

        if (artifactKind is OciArtifactKind.Image or OciArtifactKind.Index)
        {
            yield return BuildDockerPull(host, repository, reference);
            yield return BuildOrasPull(host, repository, reference, plainHttp);
            yield return BuildCranePull(host, repository, reference, plainHttp);
            yield break;
        }

        yield return BuildOrasPull(host, repository, reference, plainHttp);
    }

    private static string BuildHelmReference(string host, string repository, string reference) =>
        reference.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? $"oci://{host}/{repository}@{reference}"
            : $"oci://{host}/{repository}";

    private static string BuildHelmVersionArgument(string reference) =>
        reference.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $" --version {reference}";

    private static string BuildCosignRegistryOption(bool allowHttpRegistry) =>
        allowHttpRegistry ? " --allow-http-registry" : string.Empty;

    private static string BuildHelmRegistryOption(bool plainHttp) =>
        plainHttp ? " --plain-http" : string.Empty;

    private static string BuildOrasRegistryOption(bool plainHttp) =>
        plainHttp ? " --plain-http" : string.Empty;

    private static string BuildCraneRegistryOption(bool plainHttp) =>
        plainHttp ? " --insecure" : string.Empty;

    private static string BuildReference(string host, string repository, string reference)
    {
        var separator = reference.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? "@" : ":";
        return $"{host}/{repository}{separator}{reference}";
    }
}
