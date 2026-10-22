using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Registry.Npm;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Services;

public sealed partial class DevelopmentDemoDataSeeder
{
    private static readonly string[] DemoVersions = ["1.0.0", "1.1.0"];
    private const string NuGetDemoId = "AvantiPoint.Demo.Greetings";
    private const string NpmDemoName = "@avantipoint/demo-client";

    private async Task SeedNuGetAsync(CancellationToken cancellationToken)
    {
        if (!feedRegistry.Surfaces.Any(s => s.Protocol == FeedProtocol.NuGet)) return;
        foreach (var version in DemoVersions)
        {
            if (await context.Packages.AnyAsync(p => p.Id == NuGetDemoId && p.NormalizedVersionString == version, cancellationToken)) continue;
            using var package = new MemoryStream();
            using (var zip = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
            {
                AddEntry(zip, $"{NuGetDemoId}.nuspec", $$"""
                    <?xml version="1.0" encoding="utf-8"?>
                    <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata>
                      <id>{{NuGetDemoId}}</id><version>{{version}}</version><authors>AvantiPoint</authors>
                      <description>A tiny offline demonstration library for package browsing, decompilation and version comparisons. Not a production dependency.</description>
                      <license type="expression">MIT</license><readme>README.md</readme>
                      <tags>demo greetings dotnet</tags><dependencies><group targetFramework="net10.0" /></dependencies>
                    </metadata></package>
                    """);
                AddEntry(zip, "README.md", $"# Demo Greetings\n\nVersion **{version}** is a small, self-contained .NET 10 library generated only for development feeds.\n\n```csharp\nvar message = AvantiPoint.Demo.Greetings.Hello();\n```\n\nCompare versions 1.0.0 and 1.1.0 to inspect the method implementation and included source. Do not use this demonstration package in production.\n");
                var greeting = version == "1.0.0" ? "Hello from AvantiPoint" : "Hello from the unified package feed";
                AddEntry(zip, "src/Greetings.cs", $"namespace AvantiPoint.Demo;\npublic static class Greetings\n{{\n    public static string Hello() => \"{greeting}\";\n}}\n");
                using var entry = zip.CreateEntry($"lib/net10.0/{NuGetDemoId}.dll").Open();
                entry.Write(BuildDemoAssembly(version, greeting));
            }
            package.Position = 0;
            var result = await services.GetRequiredService<IPackageIndexingService>().IndexAsync(package, cancellationToken);
            if (result.Status is not (PackageIndexingStatus.Success or PackageIndexingStatus.PackageAlreadyExists))
                throw new InvalidOperationException($"Could not index development NuGet package {NuGetDemoId}/{version}.");
            logger.LogInformation("Seeded development NuGet package {Package}/{Version}", NuGetDemoId, version);
        }
    }

    // Reference the public .NET contract, not the host's System.Private.CoreLib.
    // Emitting this tiny library avoids downloading a compiler or requiring an SDK at runtime.
    internal static byte[] BuildDemoAssembly(string version, string greeting)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString(NuGetDemoId + ".dll"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        metadata.AddAssembly(metadata.GetOrAddString(NuGetDemoId), Version.Parse(version + ".0"), default, default, 0, AssemblyHashAlgorithm.Sha256);
        var runtime = metadata.AddAssemblyReference(metadata.GetOrAddString("System.Runtime"), new Version(10, 0, 0, 0), default,
            metadata.GetOrAddBlob(Convert.FromHexString("B03F5F7F11D50A3A")), 0, default);
        var objectType = metadata.AddTypeReference(runtime, metadata.GetOrAddString("System"), metadata.GetOrAddString("Object"));
        metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        metadata.AddTypeDefinition(TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
            metadata.GetOrAddString("AvantiPoint.Demo"), metadata.GetOrAddString("Greetings"), objectType,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));

        var il = new BlobBuilder();
        var code = new InstructionEncoder(new BlobBuilder());
        code.LoadString(metadata.GetOrAddUserString(greeting));
        code.OpCode(ILOpCode.Ret);
        var bodyOffset = new MethodBodyStreamEncoder(il).AddMethodBody(code, maxStack: 1);
        var signature = new BlobBuilder();
        new BlobEncoder(signature).MethodSignature().Parameters(0, result => result.Type().String(), _ => { });
        metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
            MethodImplAttributes.IL | MethodImplAttributes.Managed, metadata.GetOrAddString("Hello"), metadata.GetOrAddBlob(signature),
            bodyOffset, MetadataTokens.ParameterHandle(1));
        var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata), il);
        var output = new BlobBuilder();
        pe.Serialize(output);
        return output.ToArray();
    }

    private async Task SeedNpmAsync(CancellationToken cancellationToken)
    {
        var registration = feedRegistry.TryGetNpmSurface();
        if (registration is null) return;
        foreach (var version in DemoVersions)
        {
            if (await context.NpmVersions.AnyAsync(v => v.FeedId == feedRegistry.Feed.FeedId && v.Package.Name == NpmDemoName && v.Version == version, cancellationToken)) continue;
            var readme = $"# AvantiPoint Demo Client\n\nA dependency-free CommonJS package for exploring this development feed. Version **{version}**.\n\n```sh\nnpm install {NpmDemoName}@{version}\n```\n\n```js\nconst {{ hello }} = require('{NpmDemoName}');\nconsole.log(hello());\n```\n\nCompare versions to see source and package metadata changes. Not a production dependency.\n";
            var metadata = new JsonObject
            {
                ["name"] = NpmDemoName, ["version"] = version, ["description"] = "A small offline demo of npm package discovery, installation and version comparisons.",
                ["license"] = "MIT", ["author"] = "AvantiPoint", ["main"] = "index.cjs", ["types"] = "index.d.ts",
                ["keywords"] = new JsonArray("demo", "packages", "greetings"), ["engines"] = new JsonObject { ["node"] = ">=18" },
                ["readme"] = readme,
            };
            var source = version == "1.0.0" ? "exports.hello = () => 'Hello from AvantiPoint';\n"
                : "exports.hello = (name = 'developer') => 'Hello, ' + name + ', from the unified package feed';\n";
            var files = new Dictionary<string, string>
            {
                ["package/package.json"] = metadata.ToJsonString(), ["package/README.md"] = readme,
                ["package/index.cjs"] = source, ["package/index.d.ts"] = version == "1.0.0" ? "export function hello(): string;\n" : "export function hello(name?: string): string;\n",
                ["package/LICENSE"] = """
                    MIT License

                    Copyright (c) AvantiPoint

                    Permission is hereby granted, free of charge, to any person obtaining a copy
                    of this software and associated documentation files (the "Software"), to deal
                    in the Software without restriction, including without limitation the rights
                    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
                    copies of the Software, and to permit persons to whom the Software is
                    furnished to do so, subject to the following conditions:

                    The above copyright notice and this permission notice shall be included in all
                    copies or substantial portions of the Software.

                    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
                    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
                    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
                    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
                    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
                    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
                    SOFTWARE.
                    """,
            };
            var archive = CreateTar(files, gzip: true);
            await using var content = new MemoryStream(archive, writable: false);
            await services.GetRequiredService<INpmPackageService>().PublishAsync(feedRegistry.Feed.FeedId, NpmDemoName, version, content, metadata,
                new Uri($"http://localhost{registration.RoutePrefix.TrimEnd('/')}/"), cancellationToken);
            logger.LogInformation("Seeded development npm package {Package}@{Version}", NpmDemoName, version);
        }
    }

    private async Task SeedDockerImagesAsync(SurfaceContext surface, CancellationToken cancellationToken)
    {
        foreach (var version in DemoVersions)
        {
            if (await context.OciTags.AnyAsync(t => t.FeedId == surface.FeedId && t.OciSegment == surface.OciSegment
                && t.Repository.Name == DockerDemoRepositoryName && t.Tag == version, cancellationToken)) continue;
            var files = new Dictionary<string, string>
            {
                ["README.md"] = $"# Unified feed Docker demo\n\nVersion {version}. This scratch-based image contains documentation for pull, metadata, layer and version-diff exploration. It has no executable entry point.\n",
            };
            var layer = CreateTar(files, gzip: false);
            var config = JsonSerializer.SerializeToUtf8Bytes(new
            {
                architecture = "amd64", os = "linux", created = "2026-09-12T00:00:00Z",
                config = new { Env = new[] { $"DEMO_VERSION={version}" }, Labels = new Dictionary<string, string>
                {
                    ["org.opencontainers.image.title"] = "Unified feed demo", ["org.opencontainers.image.version"] = version,
                    ["org.opencontainers.image.description"] = "Scratch-based documentation image for pull and inspection. No executable entry point.",
                    ["org.opencontainers.image.licenses"] = "MIT",
                } },
                rootfs = new { type = "layers", diff_ids = new[] { Digest(layer) } },
                history = new[] { new { created = "2026-09-12T00:00:00Z", created_by = "COPY README.md /README.md" } },
            });
            var configDigest = await UploadBlobAsync(surface, DockerDemoRepositoryName, config, cancellationToken);
            var layerDigest = await UploadBlobAsync(surface, DockerDemoRepositoryName, layer, cancellationToken);
            var manifest = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 2, mediaType = "application/vnd.oci.image.manifest.v1+json",
                config = new { mediaType = "application/vnd.oci.image.config.v1+json", digest = configDigest, size = config.Length },
                layers = new[] { new { mediaType = "application/vnd.oci.image.layer.v1.tar", digest = layerDigest, size = layer.Length } },
                annotations = new Dictionary<string, string> { ["org.opencontainers.image.description"] = "Documentation-only demo image for exploring the registry.", ["org.opencontainers.image.version"] = version },
            });
            await using var content = new MemoryStream(manifest, writable: false);
            await registryService.PutManifestAsync(surface, DockerDemoRepositoryName, version, "application/vnd.oci.image.manifest.v1+json", content, cancellationToken);
            if (version == DemoVersions[^1])
            {
                content.Position = 0;
                await registryService.PutManifestAsync(surface, DockerDemoRepositoryName, "latest", "application/vnd.oci.image.manifest.v1+json", content, cancellationToken);
            }
            logger.LogInformation("Seeded development Docker image {Repository}:{Version}", DockerDemoRepositoryName, version);
        }
    }

    private static string Digest(byte[] data) => "sha256:" + Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    private static void AddEntry(ZipArchive zip, string path, string value)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(value);
    }
    private static byte[] CreateTar(Dictionary<string, string> files, bool gzip)
    {
        using var output = new MemoryStream();
        using (var compressed = gzip ? new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true) : null)
        using (var writer = new TarWriter((Stream?)compressed ?? output, TarEntryFormat.Pax, leaveOpen: true))
        {
            foreach (var (path, content) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content), writable: false);
                writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, path) { DataStream = stream, ModificationTime = DateTimeOffset.UnixEpoch });
            }
        }
        return output.ToArray();
    }
}
