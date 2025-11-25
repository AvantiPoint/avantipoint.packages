namespace AvantiPoint.Packages.Core.Signing;

public static class SigningProviderNames
{
    public const string Null = "Null";
    public const string SelfSigned = "SelfSigned";
    public const string StoredCertificate = "StoredCertificate";
    public const string AzureKeyVault = "AzureKeyVault";
    public const string AwsKms = "AwsKms";
    public const string AwsSigner = "AwsSigner";
    public const string GcpKms = "GcpKms";
    public const string GcpHsm = "GcpHsm";
}


