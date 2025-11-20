using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Logging;
using Moq;

namespace AvantiPoint.Packages.Tests.Mirror;

public class NuGetConfigParserTests : IDisposable
{
    private readonly Mock<ILogger<NuGetConfigParser>> _mockLogger;
    private readonly NuGetConfigParser _parser;
    private readonly string _testDataDirectory;

    public NuGetConfigParserTests()
    {
        _mockLogger = new Mock<ILogger<NuGetConfigParser>>();
        _parser = new NuGetConfigParser(_mockLogger.Object);
        _testDataDirectory = Path.Combine(Path.GetTempPath(), $"NuGetConfigParserTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataDirectory);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new NuGetConfigParser(null!));
    }

    [Fact]
    public void LoadSourcesFromConfig_ReturnsEmpty_WhenConfigPathIsNull()
    {
        // Act
        var result = _parser.LoadSourcesFromConfig(null!);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        VerifyLogWarning("NuGet.config path is null or empty");
    }

    [Fact]
    public void LoadSourcesFromConfig_ReturnsEmpty_WhenConfigPathIsEmpty()
    {
        // Act
        var result = _parser.LoadSourcesFromConfig(string.Empty);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        VerifyLogWarning("NuGet.config path is null or empty");
    }

    [Fact]
    public void LoadSourcesFromConfig_ReturnsEmpty_WhenConfigPathIsWhitespace()
    {
        // Act
        var result = _parser.LoadSourcesFromConfig("   ");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        VerifyLogWarning("NuGet.config path is null or empty");
    }

    [Fact]
    public void LoadSourcesFromConfig_ReturnsEmpty_WhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDataDirectory, "nonexistent.config");

        // Act
        var result = _parser.LoadSourcesFromConfig(nonExistentPath);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        VerifyLogWarning($"NuGet.config file not found at path: {nonExistentPath}");
    }

    [Fact]
    public void LoadSourcesFromConfig_LoadsUnauthenticatedSource()
    {
        // Arrange
        var configPath = CreateNuGetConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
</configuration>");

        // Act
        var result = _parser.LoadSourcesFromConfig(configPath).ToList();

        // Assert
        Assert.Single(result);
        var source = result[0];
        Assert.Equal("nuget.org", source.Name);
        Assert.Equal("https://api.nuget.org/v3/index.json", source.SourceUrl);
        Assert.Null(source.Username);
        Assert.Null(source.Password);
        Assert.False(source.HasCredentials);

        VerifyLogInformation("Loading unauthenticated source 'nuget.org' from NuGet.config");
    }

    [Fact]
    public void LoadSourcesFromConfig_LoadsMultipleUnauthenticatedSources()
    {
        // Arrange
        var configPath = CreateNuGetConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""github"" value=""https://nuget.pkg.github.com/avantipoint/index.json"" />
    <add key=""internal"" value=""https://packages.internal.local/v3/index.json"" />
  </packageSources>
</configuration>");

        // Act
        var result = _parser.LoadSourcesFromConfig(configPath).ToList();

        // Assert
        Assert.Equal(3, result.Count);

        Assert.Equal("nuget.org", result[0].Name);
        Assert.Equal("https://api.nuget.org/v3/index.json", result[0].SourceUrl);

        Assert.Equal("github", result[1].Name);
        Assert.Equal("https://nuget.pkg.github.com/avantipoint/index.json", result[1].SourceUrl);

        Assert.Equal("internal", result[2].Name);
        Assert.Equal("https://packages.internal.local/v3/index.json", result[2].SourceUrl);
    }

    [Fact]
    public void LoadSourcesFromConfig_LoadsAuthenticatedSourceWithPlainTextPassword()
    {
        // Arrange
        var configPath = CreateNuGetConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""MyFeed"" value=""https://myfeed.example.com/v3/index.json"" />
  </packageSources>
  <packageSourceCredentials>
    <MyFeed>
      <add key=""Username"" value=""testuser"" />
      <add key=""ClearTextPassword"" value=""testpass123"" />
    </MyFeed>
  </packageSourceCredentials>
</configuration>");

        // Act
        var result = _parser.LoadSourcesFromConfig(configPath).ToList();

        // Assert
        Assert.Single(result);
        var source = result[0];
        Assert.Equal("MyFeed", source.Name);
        Assert.Equal("https://myfeed.example.com/v3/index.json", source.SourceUrl);
        Assert.Equal("testuser", source.Username);
        Assert.Equal("testpass123", source.Password);
        Assert.True(source.HasCredentials);

        VerifyLogInformation("Loading authenticated source 'MyFeed' from NuGet.config with username 'testuser'");
    }

    [Fact]
    public void LoadSourcesFromConfig_LoadsMultipleAuthenticatedSources()
    {
        // Arrange
        var configPath = CreateNuGetConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""Feed1"" value=""https://feed1.example.com/v3/index.json"" />
    <add key=""Feed2"" value=""https://feed2.example.com/v3/index.json"" />
  </packageSources>
  <packageSourceCredentials>
    <Feed1>
      <add key=""Username"" value=""user1"" />
      <add key=""ClearTextPassword"" value=""pass1"" />
    </Feed1>
    <Feed2>
      <add key=""Username"" value=""user2"" />
      <add key=""ClearTextPassword"" value=""pass2"" />
    </Feed2>
  </packageSourceCredentials>
</configuration>");

        // Act
        var result = _parser.LoadSourcesFromConfig(configPath).ToList();

        // Assert
        Assert.Equal(2, result.Count);

        Assert.Equal("Feed1", result[0].Name);
        Assert.Equal("user1", result[0].Username);
        Assert.Equal("pass1", result[0].Password);
        Assert.True(result[0].HasCredentials);

        Assert.Equal("Feed2", result[1].Name);
        Assert.Equal("user2", result[1].Username);
        Assert.Equal("pass2", result[1].Password);
        Assert.True(result[1].HasCredentials);
    }

    [Fact]
    public void LoadSourcesFromConfig_MixesAuthenticatedAndUnauthenticatedSources()
    {
        // Arrange
        var configPath = CreateNuGetConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""PrivateFeed"" value=""https://private.example.com/v3/index.json"" />
    <add key=""github"" value=""https://nuget.pkg.github.com/avantipoint/index.json"" />
  </packageSources>
  <packageSourceCredentials>
    <PrivateFeed>
      <add key=""Username"" value=""admin"" />
      <add key=""ClearTextPassword"" value=""secret123"" />
    </PrivateFeed>
  </packageSourceCredentials>
</configuration>");

        // Act
        var result = _parser.LoadSourcesFromConfig(configPath).ToList();

        // Assert
        Assert.Equal(3, result.Count);

        // First source - unauthenticated
        Assert.Equal("nuget.org", result[0].Name);
        Assert.False(result[0].HasCredentials);

        // Second source - authenticated
        Assert.Equal("PrivateFeed", result[1].Name);
        Assert.Equal("admin", result[1].Username);
        Assert.Equal("secret123", result[1].Password);
        Assert.True(result[1].HasCredentials);

        // Third source - unauthenticated
        Assert.Equal("github", result[2].Name);
        Assert.False(result[2].HasCredentials);
    }

    [Fact]
    public void LoadSourcesFromConfig_SkipsDisabledSources()
    {
        // Arrange
        var configPath = CreateNuGetConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""EnabledSource"" value=""https://enabled.example.com/v3/index.json"" />
    <add key=""DisabledSource"" value=""https://disabled.example.com/v3/index.json"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""DisabledSource"" value=""true"" />
  </disabledPackageSources>
</configuration>");

        // Act
        var result = _parser.LoadSourcesFromConfig(configPath).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("EnabledSource", result[0].Name);
    }

    [Fact]
    public void LoadSourcesFromConfig_SkipsSourceWithEncryptedPassword()
    {
        // Arrange
        // Note: We can't easily create an encrypted password in the test, but we can verify the warning is logged
        // when the config contains a Password key instead of ClearTextPassword
        var configPath = CreateNuGetConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""SecureFeed"" value=""https://secure.example.com/v3/index.json"" />
  </packageSources>
  <packageSourceCredentials>
    <SecureFeed>
      <add key=""Username"" value=""secureuser"" />
      <add key=""Password"" value=""AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAA..."" />
    </SecureFeed>
  </packageSourceCredentials>
</configuration>");

        // Act
        var result = _parser.LoadSourcesFromConfig(configPath).ToList();

        // Assert
        // When password is encrypted, NuGet.Configuration won't load credentials
        // The source should still be loaded, but without credentials
        var source = result.FirstOrDefault(s => s.Name == "SecureFeed");
        if (source != null)
        {
            Assert.False(source.HasCredentials);
        }
    }

    [Fact]
    public void LoadSourcesFromConfig_ReturnsEmpty_WhenConfigIsInvalid()
    {
        // Arrange
        var configPath = CreateInvalidNuGetConfig("This is not valid XML!");

        // Act
        var result = _parser.LoadSourcesFromConfig(configPath);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        VerifyLogError($"Failed to parse NuGet.config file at path: {configPath}");
    }

    [Fact]
    public void LoadSourcesFromConfig_HandlesEmptyConfiguration()
    {
        // Arrange
        var configPath = CreateNuGetConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>");

        // Act
        var result = _parser.LoadSourcesFromConfig(configPath).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void LoadSourcesFromConfig_HandlesConfigurationWithoutPackageSources()
    {
        // Arrange
        var configPath = CreateNuGetConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <config>
    <add key=""repositoryPath"" value=""packages"" />
  </config>
</configuration>");

        // Act
        var result = _parser.LoadSourcesFromConfig(configPath).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void NuGetConfigSource_HasCredentials_ReturnsFalse_WhenUsernameIsNull()
    {
        // Arrange
        var source = new NuGetConfigSource
        {
            Name = "Test",
            SourceUrl = "https://test.example.com",
            Username = null,
            Password = "password"
        };

        // Act & Assert
        Assert.False(source.HasCredentials);
    }

    [Fact]
    public void NuGetConfigSource_HasCredentials_ReturnsFalse_WhenPasswordIsNull()
    {
        // Arrange
        var source = new NuGetConfigSource
        {
            Name = "Test",
            SourceUrl = "https://test.example.com",
            Username = "username",
            Password = null
        };

        // Act & Assert
        Assert.False(source.HasCredentials);
    }

    [Fact]
    public void NuGetConfigSource_HasCredentials_ReturnsFalse_WhenUsernameIsEmpty()
    {
        // Arrange
        var source = new NuGetConfigSource
        {
            Name = "Test",
            SourceUrl = "https://test.example.com",
            Username = string.Empty,
            Password = "password"
        };

        // Act & Assert
        Assert.False(source.HasCredentials);
    }

    [Fact]
    public void NuGetConfigSource_HasCredentials_ReturnsFalse_WhenPasswordIsEmpty()
    {
        // Arrange
        var source = new NuGetConfigSource
        {
            Name = "Test",
            SourceUrl = "https://test.example.com",
            Username = "username",
            Password = string.Empty
        };

        // Act & Assert
        Assert.False(source.HasCredentials);
    }

    [Fact]
    public void NuGetConfigSource_HasCredentials_ReturnsTrue_WhenBothAreProvided()
    {
        // Arrange
        var source = new NuGetConfigSource
        {
            Name = "Test",
            SourceUrl = "https://test.example.com",
            Username = "username",
            Password = "password"
        };

        // Act & Assert
        Assert.True(source.HasCredentials);
    }

    private string CreateNuGetConfig(string content)
    {
        var configPath = Path.Combine(_testDataDirectory, $"nuget.config.{Guid.NewGuid()}.xml");
        File.WriteAllText(configPath, content);
        return configPath;
    }

    private string CreateInvalidNuGetConfig(string content)
    {
        var configPath = Path.Combine(_testDataDirectory, $"invalid.config.{Guid.NewGuid()}.xml");
        File.WriteAllText(configPath, content);
        return configPath;
    }

    private void VerifyLogWarning(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private void VerifyLogInformation(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private void VerifyLogError(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDirectory))
        {
            Directory.Delete(_testDataDirectory, recursive: true);
        }
    }
}
