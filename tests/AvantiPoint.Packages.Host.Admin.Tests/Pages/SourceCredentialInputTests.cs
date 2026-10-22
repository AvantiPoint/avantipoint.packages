using AvantiPoint.Packages.Host.Pages.Account;

namespace AvantiPoint.Packages.Host.Admin.Tests.Pages;

public class SourceCredentialInputTests
{
    [Theory]
    [InlineData("Oci")]
    [InlineData("Oci:docker")]
    [InlineData("Oci:helm")]
    public void OciRejectsUnsupportedApiKey(string ecosystem)
    {
        var input = new PackageSourcesModel.PackageSourceInput { Ecosystem = ecosystem, CredentialMode = "Token", ApiKey = "test" };
        Assert.NotEmpty(input.ValidateCredentials(false));
    }

    [Theory]
    [InlineData("None")]
    [InlineData("Keep")]
    public void UnusedSecretsCannotBeSubmitted(string mode)
    {
        var input = new PackageSourcesModel.PackageSourceInput { CredentialMode = mode, Username = "ignored", Password = "ignored", ApiKey = "ignored" };
        Assert.Empty(input.ValidateCredentials(true));
        Assert.Null(input.Username);
        Assert.Null(input.Password);
        Assert.Null(input.ApiKey);
    }

    [Fact]
    public void NewSourceCannotKeepNonexistentCredentials()
    {
        var input = new PackageSourcesModel.PackageSourceInput { CredentialMode = "Keep" };
        Assert.NotEmpty(input.ValidateCredentials(false));
    }

    [Theory]
    [InlineData("NuGet")]
    [InlineData("Npm")]
    public void TokenModeClearsBasicCredentials(string ecosystem)
    {
        var input = new PackageSourcesModel.PackageSourceInput { Ecosystem = ecosystem, CredentialMode = "Token", Username = "ignored", Password = "ignored", ApiKey = "test" };
        Assert.Empty(input.ValidateCredentials(false));
        Assert.Null(input.Username);
        Assert.Null(input.Password);
        Assert.Equal("test", input.ApiKey);
    }

    [Theory]
    [InlineData("Basic")]
    [InlineData("Token")]
    [InlineData("Invalid")]
    public void IncompleteOrUnknownAuthenticationIsRejected(string mode)
    {
        var input = new PackageSourcesModel.PackageSourceInput { CredentialMode = mode };
        Assert.NotEmpty(input.ValidateCredentials(false));
    }
}
