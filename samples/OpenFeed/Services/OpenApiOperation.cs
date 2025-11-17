using System.Collections.Generic;

namespace OpenFeed.Services;

public sealed class OpenApiOperation
{
    public required string Verb { get; init; }
    public string? Summary { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
}
