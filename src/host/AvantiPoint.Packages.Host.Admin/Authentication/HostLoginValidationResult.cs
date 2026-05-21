namespace AvantiPoint.Packages.Host.Admin.Authentication;

public sealed class HostLoginValidationResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public static HostLoginValidationResult Success() => new() { Succeeded = true };

    public static HostLoginValidationResult Fail(string message) =>
        new() { Succeeded = false, ErrorMessage = message };
}
