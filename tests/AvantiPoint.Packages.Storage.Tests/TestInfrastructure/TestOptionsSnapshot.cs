using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Storage.Tests.TestInfrastructure;

internal sealed class TestOptionsSnapshot<T>(T value) : IOptionsSnapshot<T>
    where T : class
{
    public T Value { get; } = value;

    public T Get(string? name) => Value;
}
