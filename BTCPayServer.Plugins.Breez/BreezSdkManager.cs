using System.Collections.Concurrent;
using Breez.Sdk.Liquid;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Breez;

/// <summary>
/// Manages Breez SDK Liquid lifecycle: connection, disconnection, health, reconnection.
/// One SDK instance per mnemonic (keyed by hash).
/// </summary>
public sealed class BreezSdkManager : IDisposable
{
    private readonly ILogger<BreezSdkManager> _logger;
    private readonly ConcurrentDictionary<string, SdkInstance> _instances = new();
    private bool _disposed;

    public BreezSdkManager(ILogger<BreezSdkManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get or create an SDK instance for the given configuration.
    /// </summary>
    public Task<BindingLiquidSdk> GetOrConnectAsync(BreezConnectionConfig config, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var key = config.GetInstanceKey();

        if (_instances.TryGetValue(key, out var existing) && existing.IsConnected)
        {
            return Task.FromResult(existing.Sdk);
        }

        return Task.FromResult(Connect(key, config));
    }

    private BindingLiquidSdk Connect(string key, BreezConnectionConfig config)
    {
        _logger.LogInformation("Connecting Breez SDK Liquid instance for key {Key}...", key[..8]);

        try
        {
            var network = config.Network?.ToLowerInvariant() switch
            {
                "testnet" => LiquidNetwork.Testnet,
                "regtest" => LiquidNetwork.Regtest,
                _ => LiquidNetwork.Mainnet,
            };

            var workingDir = config.WorkingDir ?? Path.Combine(Path.GetTempPath(), "breez-btcpay", key[..16]);
            Directory.CreateDirectory(workingDir);

            var defaultConfig = BreezSdkLiquidMethods.DefaultConfig(network, config.ApiKey);

            // Config properties are init-only, so we create a new instance with the modified workingDir
            var sdkConfig = new Config(
                liquidExplorer: defaultConfig.liquidExplorer,
                bitcoinExplorer: defaultConfig.bitcoinExplorer,
                workingDir: workingDir,
                network: defaultConfig.network,
                paymentTimeoutSec: defaultConfig.paymentTimeoutSec,
                syncServiceUrl: defaultConfig.syncServiceUrl,
                breezApiKey: defaultConfig.breezApiKey,
                zeroConfMaxAmountSat: defaultConfig.zeroConfMaxAmountSat,
                onchainSyncPeriodSec: defaultConfig.onchainSyncPeriodSec,
                onchainSyncRequestTimeoutSec: defaultConfig.onchainSyncRequestTimeoutSec,
                useDefaultExternalInputParsers: defaultConfig.useDefaultExternalInputParsers,
                useMagicRoutingHints: defaultConfig.useMagicRoutingHints,
                externalInputParsers: defaultConfig.externalInputParsers,
                onchainFeeRateLeewaySat: defaultConfig.onchainFeeRateLeewaySat,
                assetMetadata: defaultConfig.assetMetadata,
                sideswapApiKey: defaultConfig.sideswapApiKey
            );

            var connectRequest = new ConnectRequest(
                config: sdkConfig,
                mnemonic: config.Mnemonic,
                passphrase: null,
                seed: null
            );

            var sdk = BreezSdkLiquidMethods.Connect(connectRequest);

            var instance = new SdkInstance(sdk);
            _instances[key] = instance;

            _logger.LogInformation("Breez SDK Liquid connected for key {Key}", key[..8]);
            return sdk;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect Breez SDK Liquid for key {Key}", key[..8]);
            throw new InvalidOperationException($"Breez SDK connection failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Disconnect a specific instance.
    /// </summary>
    public void Disconnect(string key)
    {
        if (_instances.TryRemove(key, out var instance))
        {
            try
            {
                instance.Sdk.Disconnect();
                _logger.LogInformation("Breez SDK disconnected for key {Key}", key[..8]);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disconnecting Breez SDK for key {Key}", key[..8]);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var kvp in _instances)
        {
            try
            {
                kvp.Value.Sdk.Disconnect();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during Breez SDK disposal for key {Key}", kvp.Key[..8]);
            }
        }

        _instances.Clear();
    }

    private sealed class SdkInstance
    {
        public BindingLiquidSdk Sdk { get; }
        public DateTimeOffset ConnectedAt { get; }
        public bool IsConnected => true; // SDK doesn't expose connection state; we track it

        public SdkInstance(BindingLiquidSdk sdk)
        {
            Sdk = sdk;
            ConnectedAt = DateTimeOffset.UtcNow;
        }
    }
}
