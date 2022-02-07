using System.Diagnostics;
using System.Net;
using System.Text;
using AvantiPoint.Packages.Protocol.Authentication;
using MockHttp;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace AvantiPoint.Packages.Protocol.Tests
{
    public class NuGetClientTests : IAsyncLifetime
    {
        private static readonly DirectoryInfo _packagesDirectory = new DirectoryInfo("packages");

        private ITestOutputHelper _testOutput { get; }
        private MockHttpServer? _server;

        public NuGetClientTests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        public async Task DisposeAsync()
        {
            _packagesDirectory.Delete(true);
            if(_server is not null && _server.IsStarted)
            {
                await _server.StopAsync();
                _server.Dispose();
            }
        }

        public async Task InitializeAsync()
        {
            // Create the handler.
            var mockHttp = new MockHttpHandler();
            // Configure setup(s).
            mockHttp
                .When(matching => matching
                    .Method("GET")
                    .RequestUri("v3/index.json")
                )
                .Respond(File.ReadAllText(Path.Combine("Responses", "index.json")), "application/json")
                .Verifiable();

            _server = new MockHttpServer(mockHttp, "http://127.0.0.1:5001");
            await _server.StartAsync();

            _packagesDirectory.Create();
            using var client = new NuGetClient("https://api.nuget.org/v3/index.json");

            try
            {
                using var stream = await client.DownloadPackageAsync("newtonsoft.json", NuGetVersion.Parse("12.0.1"));
                var outputFile = new FileInfo(Path.Combine(_packagesDirectory.FullName, "newtonsoft.json.12.0.1.nupkg"));
                outputFile.Delete();
                using var outputStream = outputFile.Create();
                stream.CopyTo(outputStream);
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached)
                    Debugger.Break();

                _testOutput.WriteLine(ex.ToString());
            }
        }

        [Fact]
        public async Task ReturnsValidSearch()
        {
            var expectedApiKey = Guid.NewGuid().ToString();
            _server!.Handler
                .When(matching =>
                    matching.Method("GET")
                        .RequestUri("v3/search?take=20&prerelease=true&semVerLevel=2.0.0&q=prism.core")
                )
                .Respond(request =>
                {
                    var json = File.ReadAllText(Path.Combine("Responses", "search.json"));
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, Encoding.Default, "application/json")
                    };
                })
                .Verifiable();


            using var client = new NuGetClient("http://127.0.0.1:5001/v3/index.json", CredentialsProvider.Token(expectedApiKey));

            var search = await client.SearchAsync("prism.core");

            Assert.Equal(20, search.Count);
            var result = search.First();

            Assert.Equal(2, result.Authors.Count);
            Assert.Contains("Dan Siegel", result.Authors);
            Assert.Equal("Prism.Core", result.PackageId);
        }

        [Fact]
        public void DownloadedTestPackage()
        {
            var files = _packagesDirectory.GetFiles("*.nupkg");
            Assert.Single(files);
            var file = files[0];
            Assert.Equal("newtonsoft.json.12.0.1.nupkg", file.Name);
        }

        [Fact]
        public async Task UploadsPackage()
        {
            var expectedApiKey = Guid.NewGuid().ToString();
            using var packageStream = new FileInfo(Path.Combine(_packagesDirectory.FullName, "newtonsoft.json.12.0.1.nupkg")).OpenRead();
            using var expectedStream = new MemoryStream();
            packageStream.CopyTo(expectedStream);
            var expectedData = expectedStream.ToArray();
            byte[] recievedData = Array.Empty<byte>();
            _server!.Handler
                .When(matching => matching
                    .Method("POST")
                    .RequestUri("api/v2/package")
                )
                .Respond(async request =>
                {
                    await Task.CompletedTask;
                    using var contentStream = new MemoryStream();
                    var stream = request!.Content!.ReadAsStream();
                    stream.CopyTo(contentStream);

                    recievedData = contentStream.ToArray();
                    var apiKey = request.Headers.GetValues("X-NuGet-ApiKey");
                    if (apiKey is null || !apiKey.Contains(expectedApiKey))
                        return new HttpResponseMessage(HttpStatusCode.Unauthorized);

                    return new HttpResponseMessage(HttpStatusCode.Created);
                })
                .Respond(HttpStatusCode.Created)
                .Verifiable();

            using var client = new NuGetClient("http://127.0.0.1:5001/v3/index.json", CredentialsProvider.Token(expectedApiKey));
            var result = await client.UploadPackageAsync("newtonsoft.json", NuGetVersion.Parse("12.0.1"), new MemoryStream(expectedData));

            _server.Handler.Verify(matching =>
            {
                matching.RequestUri("v3/index.json");
            }, IsSent.Once());

            _server.Handler.Verify(matching =>
            {
                matching.RequestUri("api/v2/package");
                matching.Header("X-NuGet-ApiKey", expectedApiKey);
            }, IsSent.Once());
            Assert.True(result);
            Assert.NotEmpty(recievedData);

            Assert.Equal(expectedData.Length, recievedData.Length);

            for (int i = 0; i < expectedData.Length; i++)
            {
                if (expectedData[i] != recievedData[i])
                    throw new Exception("Expected Data not recieved.");
            }
        }
    }
}