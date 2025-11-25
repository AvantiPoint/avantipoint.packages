using System.Runtime.CompilerServices;
using Xunit;

namespace AvantiPoint.Packages.Integration.Tests.TestInfrastructure;

/// <summary>
/// xUnit fact attribute that only runs when Docker is available on the system.
/// </summary>
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!DockerAvailability.IsAvailable)
        {
            Skip = DockerAvailability.SkipReason ?? "Docker is not available on this system.";
        }
    }
}

