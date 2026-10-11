using System.IO.Compression;
using AvantiPoint.Packages.Registry.Native.Swift;

namespace AvantiPoint.Packages.Registry.Native.Tests;

public sealed class XcframeworkArchiveReviewTests
{
    private const string Framework = "Example.xcframework/ios-arm64/Example.framework/";
    private const string Interface = Framework + "Modules/Example.swiftmodule/arm64-apple-ios.swiftinterface";

    [Theory]
    [InlineData("../Example")]
    [InlineData("Payload")]
    public void InterfaceSymlinkCannotStandInForPublicInterface(string target)
    {
        using var archive = EditArchive(zip =>
        {
            zip.GetEntry(Interface)!.Delete();
            AddEntry(zip, Framework + "Modules/Example.swiftmodule/Payload", "not a public interface");
            AddEntry(zip, Interface, target, symlink: true);
        });
        Assert.Throws<InvalidDataException>(() => XcframeworkValidator.Validate(archive, "Example", new()));
    }

    [Theory]
    [InlineData("Resources/Icon@2x.png")]
    [InlineData("Resources/Privacy Notice.txt")]
    [InlineData("Resources/日本語.png")]
    public void SafeResourceFilenamesAreAccepted(string path)
    {
        using var archive = EditArchive(zip => AddEntry(zip, Framework + path, "resource"));
        XcframeworkValidator.Validate(archive, "Example", new());
    }

    [Theory]
    [InlineData("Resources/../Secret.txt")]
    [InlineData("Resources/%2e%2e/Secret.txt")]
    [InlineData("Resources/..\\Secret.txt")]
    [InlineData("Resources/.hidden")]
    [InlineData("Resources/C:Secret.txt")]
    [InlineData("Resources/Notice\u0001.txt")]
    public void UnsafeResourcePathsRemainRejected(string path)
    {
        using var archive = EditArchive(zip => AddEntry(zip, Framework + path, "resource"));
        Assert.Throws<InvalidDataException>(() => XcframeworkValidator.Validate(archive, "Example", new()));
    }

    [Fact]
    public void UnicodeNormalizationAliasesCannotOverwriteResourcesOnExtraction()
    {
        using var archive = EditArchive(zip =>
        {
            AddEntry(zip, Framework + "Resources/caf\u00e9.png", "first");
            AddEntry(zip, Framework + "Resources/cafe\u0301.png", "second");
        });
        Assert.Throws<InvalidDataException>(() => XcframeworkValidator.Validate(archive, "Example", new()));
    }

    [Fact]
    public void VersionedFrameworkLinksResolveToRegularBinaryAndPublicInterface()
    {
        using var archive = EditArchive(zip =>
        {
            zip.GetEntry(Framework + "Example")!.Delete();
            zip.GetEntry(Interface)!.Delete();
            AddEntry(zip, Framework + "Versions/A/Example", "binary");
            AddEntry(zip, Framework + "Versions/A/Modules/Example.swiftmodule/arm64-apple-ios.swiftinterface", "public struct Example {}");
            AddEntry(zip, Framework + "Versions/Current", "A", symlink: true);
            AddEntry(zip, Framework + "Example", "Versions/Current/Example", symlink: true);
            AddEntry(zip, Framework + "Modules", "Versions/Current/Modules", symlink: true);
        });
        XcframeworkValidator.Validate(archive, "Example", new());
    }

    private static MemoryStream EditArchive(Action<ZipArchive> edit)
    {
        var output = new MemoryStream();
        output.Write(NativeRegistryTests.Xcframework());
        output.Position = 0;
        using (var zip = new ZipArchive(output, ZipArchiveMode.Update, leaveOpen: true))
            edit(zip);
        output.Position = 0;
        return output;
    }

    private static void AddEntry(ZipArchive zip, string path, string contents, bool symlink = false)
    {
        var entry = zip.CreateEntry(path);
        entry.ExternalAttributes = (symlink ? 0xA1FF : 0x81A4) << 16;
        using var writer = new StreamWriter(entry.Open());
        writer.Write(contents);
    }
}
