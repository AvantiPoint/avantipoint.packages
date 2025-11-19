using System.Collections.Generic;

namespace SampleDataGenerator;

/// <summary>
/// Predefined list of packages to seed from NuGet.org
/// </summary>
public static class SamplePackages
{
    /// <summary>
    /// Gets the list of packages to download from NuGet.org
    /// </summary>
    public static IReadOnlyList<PackageDefinition> Packages { get; } =
    [
        // Dan Siegel's packages
        new() { PackageId = "Mobile.BuildTools", MaxVersions = 5, IncludePrerelease = true },
        new() { PackageId = "Mobile.BuildTools.Configuration", MaxVersions = 3 },
        new() { PackageId = "Prism.Core", MaxVersions = 4 },
        new() { PackageId = "Prism.Maui", MaxVersions = 3, IncludePrerelease = true },
        new() { PackageId = "Prism.DryIoc.Maui", MaxVersions = 3, IncludePrerelease = true },
        new() { PackageId = "Prism.Forms", MaxVersions = 3 },
        new() { PackageId = "Prism.DryIoc.Forms", MaxVersions = 3 },
        new() { PackageId = "AP.CrossPlatform.Auth", MaxVersions = 2, IncludePrerelease = true },
        new() { PackageId = "AP.MobileToolkit.Fonts.FontAwesome", MaxVersions = 2 },

        // Microsoft packages
        new() { PackageId = "Microsoft.Extensions.DependencyInjection", MaxVersions = 4 },
        new() { PackageId = "Microsoft.Extensions.Logging", MaxVersions = 3 },
        new() { PackageId = "Microsoft.Extensions.Configuration", MaxVersions = 3 },
        new() { PackageId = "Microsoft.AspNetCore.Components.Web", MaxVersions = 3 },
        new() { PackageId = "Microsoft.EntityFrameworkCore", MaxVersions = 3 },
        new() { PackageId = "Microsoft.EntityFrameworkCore.Sqlite", MaxVersions = 3 },
        new() { PackageId = "System.Text.Json", MaxVersions = 3 },
        
        // Popular community packages
        new() { PackageId = "Newtonsoft.Json", MaxVersions = 2 },
        new() { PackageId = "Serilog", MaxVersions = 3 },
        new() { PackageId = "DryIoc", MaxVersions = 3 },
        new() { PackageId = "Polly", MaxVersions = 3 },

        // Tools, templates, SDKs, MCP server examples
        // dotnet tool (Entity Framework CLI)
        new() { PackageId = "dotnet-ef", MaxVersions = 2, IncludePrerelease = false },
        // GitVersion dotnet tool
        new() { PackageId = "GitVersion.Tool", MaxVersions = 2, IncludePrerelease = false },
        // Aspire project templates (will show template package type)
        new() { PackageId = "Aspire.ProjectTemplates", MaxVersions = 2, IncludePrerelease = false },
        // Uno platform templates
        new() { PackageId = "Uno.Templates", MaxVersions = 2, IncludePrerelease = false },
        // Uno SDK package
        new() { PackageId = "Uno.Sdk", MaxVersions = 3, IncludePrerelease = true },
        // Aspire AppHost SDK package
        new() { PackageId = "Aspire.AppHost.Sdk", MaxVersions = 3, IncludePrerelease = true },
        // MCP server related packages (showing custom package types)
        new() { PackageId = "NuGet.Mcp.Server", MaxVersions = 2, IncludePrerelease = true },
        new() { PackageId = "Azure.Mcp", MaxVersions = 2, IncludePrerelease = true },
    ];
}
