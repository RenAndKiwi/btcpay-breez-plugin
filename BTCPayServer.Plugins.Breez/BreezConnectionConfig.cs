using System.Security.Cryptography;
using System.Text;

namespace BTCPayServer.Plugins.Breez;

/// <summary>
/// Configuration extracted from a BTCPay connection string.
/// Format: type=breez-liquid;api-key=...;mnemonic=...;network=mainnet
/// </summary>
public sealed class BreezConnectionConfig
{
    public required string ApiKey { get; init; }
    public required string Mnemonic { get; init; }
    public string? Network { get; init; }
    public string? WorkingDir { get; init; }

    /// <summary>
    /// Instance key derived from mnemonic hash, used to deduplicate SDK connections.
    /// </summary>
    public string GetInstanceKey()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Mnemonic));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Parse from a BTCPay connection string dictionary.
    /// </summary>
    public static BreezConnectionConfig Parse(Dictionary<string, string> keyValues)
    {
        if (!keyValues.TryGetValue("api-key", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            throw new FormatException("Missing required 'api-key' in breez-liquid connection string");

        if (!keyValues.TryGetValue("mnemonic", out var mnemonic) || string.IsNullOrWhiteSpace(mnemonic))
            throw new FormatException("Missing required 'mnemonic' in breez-liquid connection string");

        keyValues.TryGetValue("network", out var network);
        keyValues.TryGetValue("working-dir", out var workingDir);

        return new BreezConnectionConfig
        {
            ApiKey = apiKey.Trim(),
            Mnemonic = mnemonic.Trim(),
            Network = network?.Trim(),
            WorkingDir = workingDir?.Trim()
        };
    }
}
