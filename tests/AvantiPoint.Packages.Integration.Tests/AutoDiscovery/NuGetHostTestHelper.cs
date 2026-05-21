using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AvantiPoint.Packages;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Core.Storage;
using AvantiPoint.Packages.Database.PostgreSql;
using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Database.SqlServer;
using AvantiPoint.Packages.Integration.Tests.TestInfrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;
namespace AvantiPoint.Packages.Integration.Tests.AutoDiscovery;

internal static class NuGetHostTestHelper
{
    public static async Task<IHost> BuildHostAsync(
        IDictionary<string, string?> configuration,
        Action<NuGetApiOptions> configureOptions)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddLogging();
        builder.Services.AddNuGetPackageApi(configureOptions);
        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    public static async Task DisposeHostAsync(IHost host)
    {
        try
        {
            await host.StopAsync();
        }
        finally
        {
            host.Dispose();
        }
    }

    public static string CreateStoragePath()
    {
        var path = Path.Combine(Path.GetTempPath(), "ap-storage", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    public static void DeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}

