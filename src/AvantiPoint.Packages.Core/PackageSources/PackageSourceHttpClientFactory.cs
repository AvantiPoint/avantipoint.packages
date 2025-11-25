#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;

namespace AvantiPoint.Packages.Core;

internal static class PackageSourceHttpClientFactory
{
    private static readonly string UserAgent;

    static PackageSourceHttpClientFactory()
    {
        var entryAssembly = Assembly.GetEntryAssembly() ?? typeof(PackageSourceHttpClientFactory).Assembly;
        var assemblyName = entryAssembly.GetName().Name;
        var assemblyVersion = entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        UserAgent = $"{assemblyName}/{assemblyVersion}";
    }

    public static HttpClient Create(PackageSource source, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(source);

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        var client = new HttpClient(handler)
        {
            Timeout = timeout
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

        if (!string.IsNullOrEmpty(source.Username) && !string.IsNullOrEmpty(source.Password))
        {
            var creds = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{source.Username}:{source.Password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
        }

        if (!string.IsNullOrEmpty(source.ApiKey))
        {
            client.DefaultRequestHeaders.Add("X-NuGet-ApiKey", source.ApiKey);
        }

        return client;
    }
}

