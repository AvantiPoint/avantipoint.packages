using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Configuration;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Parses NuGet.config files to extract package sources with credentials.
/// </summary>
public class NuGetConfigParser
{
    private readonly ILogger<NuGetConfigParser> _logger;

    public NuGetConfigParser(ILogger<NuGetConfigParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads package sources from a NuGet.config file.
    /// Sources with encrypted passwords are ignored.
    /// </summary>
    /// <param name="configPath">Path to the NuGet.config file</param>
    /// <returns>Collection of parsed package sources with credentials</returns>
    public IEnumerable<NuGetConfigSource> LoadSourcesFromConfig(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            _logger.LogWarning("NuGet.config path is null or empty");
            return Enumerable.Empty<NuGetConfigSource>();
        }

        if (!File.Exists(configPath))
        {
            _logger.LogWarning("NuGet.config file not found at path: {ConfigPath}", configPath);
            return Enumerable.Empty<NuGetConfigSource>();
        }

        try
        {
            var configDirectory = Path.GetDirectoryName(configPath);
            var settings = Settings.LoadSpecificSettings(configDirectory, Path.GetFileName(configPath));

            var packageSourceProvider = new PackageSourceProvider(settings);
            var sources = packageSourceProvider.LoadPackageSources();

            var result = new List<NuGetConfigSource>();

            foreach (var source in sources.Where(s => s.IsEnabled))
            {
                var credentials = source.Credentials;

                if (credentials != null)
                {
                    // Check if password is encrypted
                    if (credentials.IsPasswordClearText)
                    {
                        _logger.LogInformation(
                            "Loading authenticated source '{SourceName}' from NuGet.config with username '{Username}'",
                            source.Name,
                            credentials.Username);

                        result.Add(new NuGetConfigSource
                        {
                            Name = source.Name,
                            SourceUrl = source.Source,
                            Username = credentials.Username,
                            Password = credentials.PasswordText
                        });
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Skipping source '{SourceName}' from NuGet.config - password is encrypted. " +
                            "Only plain-text passwords are supported for upstream sources.",
                            source.Name);
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "Loading unauthenticated source '{SourceName}' from NuGet.config",
                        source.Name);

                    result.Add(new NuGetConfigSource
                    {
                        Name = source.Name,
                        SourceUrl = source.Source
                    });
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse NuGet.config file at path: {ConfigPath}", configPath);
            return Enumerable.Empty<NuGetConfigSource>();
        }
    }
}

/// <summary>
/// Represents a package source loaded from NuGet.config
/// </summary>
public class NuGetConfigSource
{
    public string Name { get; set; }
    public string SourceUrl { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public bool HasCredentials => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
}
