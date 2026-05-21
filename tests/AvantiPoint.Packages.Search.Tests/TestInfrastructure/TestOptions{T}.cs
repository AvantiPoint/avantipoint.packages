using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Search.Tests.TestInfrastructure;

internal sealed class TestOptions<T> : IOptions<T> where T : class
{
    public TestOptions(T value) => Value = value;

    public T Value { get; }
}
