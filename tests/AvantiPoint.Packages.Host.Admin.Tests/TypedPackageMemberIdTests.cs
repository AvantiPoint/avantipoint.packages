using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services;

namespace AvantiPoint.Packages.Host.Admin.Tests;

public sealed class TypedPackageMemberIdTests
{
    [Theory]
    [InlineData("Good.Package", PublishTargetProtocol.NuGet, "Good.Package")]
    [InlineData("nuget:Good.Package", PublishTargetProtocol.NuGet, "Good.Package")]
    public void Parse_NuGetIds(string input, PublishTargetProtocol protocol, string artifact)
    {
        var typed = TypedPackageMemberId.Parse(input);
        Assert.Equal(protocol, typed.Protocol);
        Assert.Equal(artifact, typed.ArtifactName);
        Assert.Equal(input, typed.OriginalId);
    }

    [Fact]
    public void Parse_PrefixedNpmId()
    {
        var input = "np" + "m:left-pad";
        var typed = TypedPackageMemberId.Parse(input);
        Assert.Equal(PublishTargetProtocol.Npm, typed.Protocol);
        Assert.Equal("left-pad", typed.ArtifactName);
    }

    [Fact]
    public void Parse_ScopedNpmId()
    {
        var input = "@scope/pkg";
        var typed = TypedPackageMemberId.Parse(input);
        Assert.Equal(PublishTargetProtocol.Npm, typed.Protocol);
        Assert.Equal(input, typed.ArtifactName);
    }

    [Fact]
    public void Parse_SlashIsOci()
    {
        var typed = TypedPackageMemberId.Parse("owner/image");
        Assert.Equal(PublishTargetProtocol.Oci, typed.Protocol);
        Assert.Equal("owner/image", typed.ArtifactName);
    }

    [Fact]
    public void Parse_PrefixedOciId()
    {
        var input = "oc" + "i:owner/image";
        var typed = TypedPackageMemberId.Parse(input);
        Assert.Equal(PublishTargetProtocol.Oci, typed.Protocol);
        Assert.Equal("owner/image", typed.ArtifactName);
    }

    [Fact]
    public void SkipReason_IncludesBothProtocols()
    {
        var reason = TypedPackageMemberId.SkipReason(PublishTargetProtocol.Npm, PublishTargetProtocol.NuGet);
        Assert.Contains("cannot be pushed to a", reason);
        Assert.Contains("NuGet", reason);
    }
}
