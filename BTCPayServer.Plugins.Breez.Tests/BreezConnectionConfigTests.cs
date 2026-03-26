using Xunit;

namespace BTCPayServer.Plugins.Breez.Tests;

public class BreezConnectionConfigTests
{
    [Fact]
    public void Parse_ValidConnectionString_ReturnsConfig()
    {
        var keyValues = new Dictionary<string, string>
        {
            { "type", "breez-liquid" },
            { "api-key", "test-api-key-123" },
            { "mnemonic", "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about" },
            { "network", "testnet" }
        };

        var config = BreezConnectionConfig.Parse(keyValues);

        Assert.Equal("test-api-key-123", config.ApiKey);
        Assert.Equal("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about", config.Mnemonic);
        Assert.Equal("testnet", config.Network);
    }

    [Fact]
    public void Parse_MissingApiKey_Throws()
    {
        var keyValues = new Dictionary<string, string>
        {
            { "type", "breez-liquid" },
            { "mnemonic", "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about" }
        };

        Assert.Throws<FormatException>(() => BreezConnectionConfig.Parse(keyValues));
    }

    [Fact]
    public void Parse_MissingMnemonic_Throws()
    {
        var keyValues = new Dictionary<string, string>
        {
            { "type", "breez-liquid" },
            { "api-key", "test-api-key-123" }
        };

        Assert.Throws<FormatException>(() => BreezConnectionConfig.Parse(keyValues));
    }

    [Fact]
    public void Parse_EmptyApiKey_Throws()
    {
        var keyValues = new Dictionary<string, string>
        {
            { "type", "breez-liquid" },
            { "api-key", "   " },
            { "mnemonic", "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about" }
        };

        Assert.Throws<FormatException>(() => BreezConnectionConfig.Parse(keyValues));
    }

    [Fact]
    public void GetInstanceKey_SameMnemonic_SameKey()
    {
        var config1 = new BreezConnectionConfig
        {
            ApiKey = "key1",
            Mnemonic = "test mnemonic phrase"
        };

        var config2 = new BreezConnectionConfig
        {
            ApiKey = "key2",
            Mnemonic = "test mnemonic phrase"
        };

        Assert.Equal(config1.GetInstanceKey(), config2.GetInstanceKey());
    }

    [Fact]
    public void GetInstanceKey_DifferentMnemonic_DifferentKey()
    {
        var config1 = new BreezConnectionConfig
        {
            ApiKey = "key1",
            Mnemonic = "test mnemonic phrase one"
        };

        var config2 = new BreezConnectionConfig
        {
            ApiKey = "key1",
            Mnemonic = "test mnemonic phrase two"
        };

        Assert.NotEqual(config1.GetInstanceKey(), config2.GetInstanceKey());
    }

    [Fact]
    public void Parse_OptionalFieldsMissing_DefaultsToNull()
    {
        var keyValues = new Dictionary<string, string>
        {
            { "type", "breez-liquid" },
            { "api-key", "test-key" },
            { "mnemonic", "test mnemonic" }
        };

        var config = BreezConnectionConfig.Parse(keyValues);

        Assert.Null(config.Network);
        Assert.Null(config.WorkingDir);
    }

    [Fact]
    public void Parse_TrimsWhitespace()
    {
        var keyValues = new Dictionary<string, string>
        {
            { "type", "breez-liquid" },
            { "api-key", "  test-key  " },
            { "mnemonic", "  test mnemonic  " }
        };

        var config = BreezConnectionConfig.Parse(keyValues);

        Assert.Equal("test-key", config.ApiKey);
        Assert.Equal("test mnemonic", config.Mnemonic);
    }
}
