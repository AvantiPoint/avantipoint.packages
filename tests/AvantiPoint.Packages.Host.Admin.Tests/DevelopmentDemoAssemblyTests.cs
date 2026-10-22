using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using AvantiPoint.Packages.Host.Services;

namespace AvantiPoint.Packages.Host.Admin.Tests;

public class DevelopmentDemoAssemblyTests
{
    [Theory]
    [InlineData("1.0.0", "Hello from AvantiPoint")]
    [InlineData("1.1.0", "Hello from the unified package feed")]
    public void GeneratedLibraryUsesPublicContractsAndExecutes(string version, string greeting)
    {
        var bytes = DevelopmentDemoDataSeeder.BuildDemoAssembly(version, greeting);
        using var stream = new MemoryStream(bytes);
        using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
        var metadata = pe.GetMetadataReader();
        Assert.Equal(["System.Runtime"], metadata.AssemblyReferences.Select(r => metadata.GetString(metadata.GetAssemblyReference(r).Name)));
        var context = new AssemblyLoadContext(Guid.NewGuid().ToString(), isCollectible: true);
        try
        {
            stream.Position = 0;
            var assembly = context.LoadFromStream(stream);
            Assert.Equal(greeting, assembly.GetType("AvantiPoint.Demo.Greetings")!.GetMethod("Hello")!.Invoke(null, null));
        }
        finally { context.Unload(); }
    }

    [Fact]
    public async Task GeneratedLibraryCanBeConsumedByTheDotNetCompiler()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Directory.CreateTempSubdirectory("ap-demo-compiler-").FullName;
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(directory, "AvantiPoint.Demo.Greetings.dll"), DevelopmentDemoDataSeeder.BuildDemoAssembly("1.1.0", "demo compiler verified"), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "Consumer.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><Reference Include="AvantiPoint.Demo.Greetings"><HintPath>AvantiPoint.Demo.Greetings.dll</HintPath></Reference></ItemGroup></Project>
                """, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>", cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "Program.cs"), "System.Console.WriteLine(AvantiPoint.Demo.Greetings.Hello());", cancellationToken);
            using var process = Process.Start(new ProcessStartInfo("dotnet", "run --project Consumer.csproj --disable-build-servers")
            {
                WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            })!;
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(1));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch { process.Kill(entireProcessTree: true); throw; }
            Assert.True(process.ExitCode == 0, await output + await error);
            Assert.Contains("demo compiler verified", await output);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
