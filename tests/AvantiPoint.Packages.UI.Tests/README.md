# AvantiPoint.Packages.UI.Tests

Integration and component tests for the sample feed UI.

## Structure

- bUnit tests (`PackageSearchComponentTests`) verify the Blazor `PackageSearch` component renders and updates.
- API tests (`SearchApiTests`) call real feed endpoints using `WebApplicationFactory`.
- A hosted service `TestPackageSeeder` seeds a small in-memory catalog of packages for deterministic results.

## Running Tests

```bash
dotnet test tests/AvantiPoint.Packages.UI.Tests/AvantiPoint.Packages.UI.Tests.csproj -c Debug
```

## Adding More Tests

- To add packages, modify `TestPackageSeeder`.
- For additional component tests, render with `TestContext` and register services via `AddNuGetSearchService`.
