using AvantiPoint.Packages.Aws;
using AvantiPoint.Packages.Ftp;
using AvantiPoint.Packages.Gcp;
using AvantiPoint.Packages.Sftp;
using Xunit;

namespace AvantiPoint.Packages.Storage.Tests;

public class StorageConnectionStringTests
{
    [Fact]
    public void Ftp_ParsesUriIntoFields()
    {
        var options = new FtpStorageOptions
        {
            ConnectionString = "ftps://user:pass@ftp.example.com:2121/packages?passive=false&connectTimeout=15",
        };

        options.ApplyConnectionString();

        Assert.True(options.UseSsl); // ftps
        Assert.Equal("ftp.example.com", options.Host);
        Assert.Equal(2121, options.Port);
        Assert.Equal("user", options.Username);
        Assert.Equal("pass", options.Password);
        Assert.Equal("/packages", options.RemotePath);
        Assert.False(options.UsePassiveMode);
        Assert.Equal(TimeSpan.FromSeconds(15), options.ConnectTimeout);
    }

    [Fact]
    public void Sftp_ParsesUriIntoFields()
    {
        var options = new SftpStorageOptions
        {
            ConnectionString = "sftp://svc@sftp.example.com/packages?privateKeyPath=/keys/id_rsa&maxConnections=8",
        };

        options.ApplyConnectionString();

        Assert.Equal("sftp.example.com", options.Host);
        Assert.Equal(22, options.Port); // default preserved when not specified
        Assert.Equal("svc", options.Username);
        Assert.Equal("/packages", options.RemotePath);
        Assert.Equal("/keys/id_rsa", options.PrivateKeyPath);
        Assert.Equal(8, options.MaxConnections);
    }

    [Fact]
    public void S3_ParsesAwsUri()
    {
        var options = new S3StorageOptions
        {
            ConnectionString = "s3://AKIA:secret@my-bucket?region=us-west-2&prefix=feed",
        };

        options.ApplyConnectionString();

        Assert.Equal("AKIA", options.AccessKey);
        Assert.Equal("secret", options.SecretKey);
        Assert.Equal("my-bucket", options.Bucket);
        Assert.Equal("us-west-2", options.Region);
        Assert.Equal("feed", options.Prefix);
        Assert.Null(options.ServiceUrl);
    }

    [Fact]
    public void S3_ParsesCompatibleEndpointAndDefaultsRegion()
    {
        var options = new S3StorageOptions
        {
            ConnectionString = "s3://minioadmin:minioadmin@packages?endpoint=http://localhost:9000&forcePathStyle=true",
        };

        options.ApplyConnectionString();

        Assert.Equal("packages", options.Bucket);
        Assert.Equal("http://localhost:9000", options.ServiceUrl);
        Assert.True(options.ForcePathStyle);
        Assert.Equal("us-east-1", options.Region); // defaulted for S3-compatible endpoints
    }

    [Fact]
    public void Gcs_ParsesUriIntoFields()
    {
        var options = new GcsStorageOptions
        {
            ConnectionString = "gs://my-bucket?credentialsPath=/keys/sa.json&prefix=packages",
        };

        options.ApplyConnectionString();

        Assert.Equal("my-bucket", options.Bucket);
        Assert.Equal("/keys/sa.json", options.CredentialsPath);
        Assert.Equal("packages", options.Prefix);
    }

    [Fact]
    public void NoConnectionString_LeavesFieldsUnchanged()
    {
        var options = new FtpStorageOptions { Host = "explicit.example.com", Username = "u", Password = "p" };

        options.ApplyConnectionString();

        Assert.Equal("explicit.example.com", options.Host);
    }

    [Fact]
    public void InvalidConnectionString_Throws()
    {
        var options = new GcsStorageOptions { ConnectionString = "not a uri" };

        Assert.Throws<InvalidOperationException>(options.ApplyConnectionString);
    }
}
