using System.Collections.Generic;

namespace OpenFeed.Services;

public sealed class OpenApiPath
{
    public required string Route { get; init; }
    public required IReadOnlyList<OpenApiOperation> Operations { get; init; }
}
