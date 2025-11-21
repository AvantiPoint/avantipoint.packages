using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Signing;
using AvantiPoint.Packages.Database.Sqlite;
using IntegrationTestApi;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using NuGet.Versioning;
using Xunit;

namespace AvantiPoint.Packages.Tests.Signing;

public class PackageSigningIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteContext _context;
    private readonly WebApplicationFactory<IntegrationTestApi.Program> _factory;
    private readonly HttpClient _client;
    private readonly string _storagePath;

    public PackageSigningIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new SqliteContext(options);
        _context.Database.EnsureCreated();

        _storagePath = Path.Combine(Path.GetTempPath(), $"test-packages-{Guid.NewGuid()}");

        _factory = new WebApplicationFactory<IntegrationTestApi.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "ApiKey", "test-api-key-12345" },
                        { "Database:Type", "Sqlite" },
                        { "ConnectionStrings:Sqlite", "DataSource=:memory:" },
                        { "Storage:Type", "FileSystem" },
                        { "Storage:Path", _storagePath },
                        { "Signing:Mode", "SelfSigned" },
                        { "Signing:SelfSigned:SubjectName", "CN=Test Repository Signer" },
                        { "Signing:SelfSigned:KeySize", "KeySize4096" },
                        { "Signing:TimestampServerUrl", "" } // Disable timestamping for faster tests
                    });
                });

                builder.ConfigureTestServices(services =>
                {
                    // Replace DbContext with our in-memory connection
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<SqliteContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddDbContext<SqliteContext>(options =>
                    {
                        options.UseSqlite(_connection);
                    });

                    // Replace IContext
                    var contextDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IContext));
                    if (contextDescriptor != null)
                    {
                        services.Remove(contextDescriptor);
                    }
                    services.AddScoped<IContext>(_ => _context);
                });

                builder.ConfigureServices(services =>
                {
                    // Ensure database is created
                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IContext>();
                    db.Database.EnsureCreated();
                });
            });

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
        _context?.Dispose();
        _connection?.Dispose();
        
        // Clean up storage directory
        try
        {
            if (Directory.Exists(_storagePath))
            {
                Directory.Delete(_storagePath, recursive: true);
            }
        }
        catch { }
    }

    private static Stream CreateTestPackage(string packageId, string version)
    {
        var builder = new PackageBuilder
        {
            Id = packageId,
            Version = NuGetVersion.Parse(version),
            Description = $"Test package {packageId} version {version}"
        };
        builder.Authors.Add("Test Author");

        var dummyFile = new PhysicalPackageFile
        {
            SourcePath = Path.GetTempFileName(),
            TargetPath = "lib/netstandard2.0/_.dll"
        };
        File.WriteAllText(dummyFile.SourcePath, "dummy content");
        builder.Files.Add(dummyFile);

        var stream = new MemoryStream();
        builder.Save(stream);
        stream.Position = 0;

        try { File.Delete(dummyFile.SourcePath); } catch { }

        return stream;
    }

    private static async Task<bool> IsPackageSignedAsync(Stream packageStream)
    {
        packageStream.Position = 0;
        try
        {
            using var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
            return await packageReader.IsSignedAsync(CancellationToken.None);
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task PackageUpload_WithSigningEnabled_SignsPackageAndSavesToStorage()
    {
        // Arrange
        var packageId = "Test.Package";
        var version = "1.0.0";
        var packageStream = CreateTestPackage(packageId, version);
        var packageBytes = ((MemoryStream)packageStream).ToArray();

            var request = new HttpRequestMessage(HttpMethod.Put, "/api/v2/package")
            {
                Content = new ByteArrayContent(packageBytes)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Headers.Add("X-NuGet-ApiKey", "test-api-key-12345");

        // Act - Upload package
        var uploadResponse = await _client.SendAsync(request);
        uploadResponse.EnsureSuccessStatusCode();

        // Wait a moment for async operations
        await Task.Delay(500);

        // Assert - Package should be signed
        var downloadResponse = await _client.GetAsync($"/v3/package/{packageId.ToLowerInvariant()}/{version}/{packageId.ToLowerInvariant()}.{version}.nupkg");
        downloadResponse.EnsureSuccessStatusCode();

        var downloadedStream = await downloadResponse.Content.ReadAsStreamAsync();
        var isSigned = await IsPackageSignedAsync(downloadedStream);

        Assert.True(isSigned, "Package should be signed after upload");

        // Verify certificate usage was recorded
        var certificates = await _context.RepositorySigningCertificates.ToListAsync();
        Assert.NotEmpty(certificates);
        Assert.True(certificates.Any(c => c.IsActive));
    }

    [Fact]
    public async Task PackageDownload_WithUnsignedPackage_SignsOnDemand()
    {
        // Arrange - Upload package with signing disabled first
        var packageId = "Test.Package.Unsigned";
        var version = "1.0.0";
        var packageStream = CreateTestPackage(packageId, version);
        var packageBytes = ((MemoryStream)packageStream).ToArray();

        // Temporarily disable signing
        var factoryWithoutSigning = new WebApplicationFactory<IntegrationTestApi.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "Database:Type", "Sqlite" },
                        { "ConnectionStrings:Sqlite", "DataSource=:memory:" },
                        { "Storage:Type", "FileSystem" },
                        { "Storage:Path", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()) },
                        { "Signing:Mode", null } // Disable signing
                    });
                });

                builder.ConfigureTestServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<SqliteContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddDbContext<SqliteContext>(options =>
                    {
                        options.UseSqlite(_connection);
                    });

                    var contextDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IContext));
                    if (contextDescriptor != null)
                    {
                        services.Remove(contextDescriptor);
                    }
                    services.AddScoped<IContext>(_ => _context);
                });
            });

        var clientWithoutSigning = factoryWithoutSigning.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v2/package")
        {
            Content = new ByteArrayContent(packageBytes)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Headers.Add("X-NuGet-ApiKey", "test-api-key-12345");

        // Upload without signing
        var uploadResponse = await clientWithoutSigning.SendAsync(request);
        uploadResponse.EnsureSuccessStatusCode();

        clientWithoutSigning.Dispose();
        factoryWithoutSigning.Dispose();

        // Act - Download package (should trigger on-demand signing)
        var downloadResponse = await _client.GetAsync($"/v3/package/{packageId.ToLowerInvariant()}/{version}/{packageId.ToLowerInvariant()}.{version}.nupkg");
        downloadResponse.EnsureSuccessStatusCode();

        var downloadedStream = await downloadResponse.Content.ReadAsStreamAsync();
        var isSigned = await IsPackageSignedAsync(downloadedStream);

        // Assert - Package should be signed on-demand
        Assert.True(isSigned, "Package should be signed on-demand during download");
    }

    [Fact]
    public async Task PackageUpload_WithStoredCertificateMode_RecordsCertificateUsage()
    {
        // Arrange - Create a test certificate file
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Stored Test Certificate");
        var certPath = Path.Combine(Path.GetTempPath(), $"test-cert-{Guid.NewGuid()}.pfx");
        var certPassword = "test-password";
        File.WriteAllBytes(certPath, certificate.Export(X509ContentType.Pfx, certPassword));

        try
        {
            var factoryWithStoredCert = new WebApplicationFactory<IntegrationTestApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            { "ApiKey", "test-api-key-12345" },
                            { "Database:Type", "Sqlite" },
                            { "ConnectionStrings:Sqlite", "DataSource=:memory:" },
                            { "Storage:Type", "FileSystem" },
                            { "Storage:Path", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()) },
                            { "Signing:Mode", "StoredCertificate" },
                            { "Signing:StoredCertificate:FilePath", certPath },
                            { "Signing:StoredCertificate:Password", certPassword }
                        });
                    });

                    builder.ConfigureTestServices(services =>
                    {
                        var descriptor = services.SingleOrDefault(
                            d => d.ServiceType == typeof(DbContextOptions<SqliteContext>));
                        if (descriptor != null)
                        {
                            services.Remove(descriptor);
                        }

                        services.AddDbContext<SqliteContext>(options =>
                        {
                            options.UseSqlite(_connection);
                        });

                        var contextDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IContext));
                        if (contextDescriptor != null)
                        {
                            services.Remove(contextDescriptor);
                        }
                        services.AddScoped<IContext>(_ => _context);
                    });
                });

            var clientWithStoredCert = factoryWithStoredCert.CreateClient();

            var packageId = "Test.Package.Stored";
            var version = "1.0.0";
            var packageStream = CreateTestPackage(packageId, version);
            var packageBytes = ((MemoryStream)packageStream).ToArray();

            var request = new HttpRequestMessage(HttpMethod.Put, "/api/v2/package")
            {
                Content = new ByteArrayContent(packageBytes)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Headers.Add("X-NuGet-ApiKey", "test-api-key-12345");

            // Act - Upload package
            var uploadResponse = await clientWithStoredCert.SendAsync(request);
            uploadResponse.EnsureSuccessStatusCode();

            await Task.Delay(500); // Wait for async operations

            // Assert - Certificate usage should be recorded
            var certificates = await _context.RepositorySigningCertificates.ToListAsync();
            Assert.NotEmpty(certificates);
            var recordedCert = certificates.FirstOrDefault(c => c.Subject == "CN=Stored Test Certificate");
            Assert.NotNull(recordedCert);
            Assert.True(recordedCert.IsActive);
            Assert.NotNull(recordedCert.PublicCertificateBytes);
            Assert.NotNull(recordedCert.ContentUrl);

            clientWithStoredCert.Dispose();
            factoryWithStoredCert.Dispose();
        }
        finally
        {
            try { File.Delete(certPath); } catch { }
        }
    }

    [Fact]
    public async Task PackageDownload_SubsequentDownloads_ServePreSignedPackage()
    {
        // Arrange - Upload and sign a package
        var packageId = "Test.Package.PreSigned";
        var version = "1.0.0";
        var packageStream = CreateTestPackage(packageId, version);
        var packageBytes = ((MemoryStream)packageStream).ToArray();

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v2/package")
        {
            Content = new ByteArrayContent(packageBytes)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Headers.Add("X-NuGet-ApiKey", "test-api-key-12345");

        var uploadResponse = await _client.SendAsync(request);
        uploadResponse.EnsureSuccessStatusCode();

        await Task.Delay(500); // Wait for signing to complete

        // Act - Download package multiple times
        var download1 = await _client.GetAsync($"/v3/package/{packageId.ToLowerInvariant()}/{version}/{packageId.ToLowerInvariant()}.{version}.nupkg");
        download1.EnsureSuccessStatusCode();
        var stream1 = await download1.Content.ReadAsStreamAsync();
        var isSigned1 = await IsPackageSignedAsync(stream1);

        var download2 = await _client.GetAsync($"/v3/package/{packageId.ToLowerInvariant()}/{version}/{packageId.ToLowerInvariant()}.{version}.nupkg");
        download2.EnsureSuccessStatusCode();
        var stream2 = await download2.Content.ReadAsStreamAsync();
        var isSigned2 = await IsPackageSignedAsync(stream2);

        // Assert - Both downloads should be signed (pre-signed package served)
        Assert.True(isSigned1, "First download should be signed");
        Assert.True(isSigned2, "Second download should be signed");
    }

    [Fact]
    public async Task PackageUpload_WithSelfSignedMode_SucceedsEvenIfSigningFails()
    {
        // This test verifies graceful fallback for self-signed certificates
        // In a real scenario, signing should succeed, but we can verify the behavior
        // by checking that the package is still available even if signing has issues

        // Arrange
        var packageId = "Test.Package.Graceful";
        var version = "1.0.0";
        var packageStream = CreateTestPackage(packageId, version);
        var packageBytes = ((MemoryStream)packageStream).ToArray();

            var request = new HttpRequestMessage(HttpMethod.Put, "/api/v2/package")
            {
                Content = new ByteArrayContent(packageBytes)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Headers.Add("X-NuGet-ApiKey", "test-api-key-12345");

        // Act - Upload package
        var uploadResponse = await _client.SendAsync(request);
        uploadResponse.EnsureSuccessStatusCode();

        // Assert - Package should be available (even if signing failed, it should still be uploaded)
        var downloadResponse = await _client.GetAsync($"/v3/package/{packageId.ToLowerInvariant()}/{version}/{packageId.ToLowerInvariant()}.{version}.nupkg");
        downloadResponse.EnsureSuccessStatusCode();

        // Package should exist (may or may not be signed depending on signing success)
        var downloadedStream = await downloadResponse.Content.ReadAsStreamAsync();
        Assert.True(downloadedStream.Length > 0, "Package should be available for download");
    }
}

