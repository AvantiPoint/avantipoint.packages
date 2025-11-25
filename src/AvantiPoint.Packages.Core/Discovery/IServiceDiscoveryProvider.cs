namespace AvantiPoint.Packages.Core.Discovery;

public interface IServiceDiscoveryProvider<T>
{
    string Name { get; }
    T GetService();
    
    /// <summary>
    /// Validates that this provider is properly configured and can create an instance.
    /// Throws an exception if configuration is invalid.
    /// </summary>
    void ValidateConfiguration();
}
