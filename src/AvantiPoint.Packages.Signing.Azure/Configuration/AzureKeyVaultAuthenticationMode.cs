#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace AvantiPoint.Packages.Signing.Azure;

public enum AzureKeyVaultAuthenticationMode
{
    /// <summary>
    /// Uses DefaultAzureCredential, which supports:
    /// - Managed Identity (when running on Azure)
    /// - Azure CLI (az login)
    /// - Visual Studio
    /// - Environment variables
    /// </summary>
    Default,

    /// <summary>
    /// Uses Managed Identity authentication (requires running on Azure with managed identity enabled).
    /// </summary>
    ManagedIdentity,

    /// <summary>
    /// Uses client secret authentication (requires TenantId, ClientId, and ClientSecret).
    /// </summary>
    ClientSecret
}

