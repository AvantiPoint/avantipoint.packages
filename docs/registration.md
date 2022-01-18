When setting up a package feed you must call the `AddNuGetPackageApi` extension in your ConfigureServices. This will register the base services. You can then configure any additional or optional services you would like including the Database provider or FileStorage type using the builder.

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddNuGetPackageApi(app =>
    {
        
    });
}
```

The configuration options will pull the Host Environment Name from the registered IHostEnvironment instance. This allows you to additionally provide specific configurations based on Environment. For instance you can use Sqlite while testing in a Development Environment and SqlServer when in a non-Development environment.