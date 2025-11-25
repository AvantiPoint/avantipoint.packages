using System.Runtime.CompilerServices;
using Xunit;

namespace AvantiPoint.Packages.Integration.Tests.TestInfrastructure;

/// <summary>
/// xUnit fact attribute that only runs on Windows.
/// Optionally requires Docker to be available.
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute(
        bool requiresDocker = false,
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "This test requires Windows.";
            return;
        }

        if (requiresDocker && !DockerAvailability.IsAvailable)
        {
            Skip = $"This test requires Windows and Docker. {DockerAvailability.SkipReason}";
        }
    }
}

