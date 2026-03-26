using BTCPayServer.Lightning;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace BTCPayServer.Plugins.Breez;

/// <summary>
/// Registers "breez-liquid" as a connection string type in BTCPay Server.
/// Connection format: type=breez-liquid;api-key=YOUR_KEY;mnemonic=YOUR_MNEMONIC;network=mainnet
/// </summary>
public sealed class BreezConnectionHandler : ILightningConnectionStringHandler
{
    private readonly BreezSdkManager _sdkManager;
    private readonly ILoggerFactory _loggerFactory;

    public BreezConnectionHandler(BreezSdkManager sdkManager, ILoggerFactory loggerFactory)
    {
        _sdkManager = sdkManager;
        _loggerFactory = loggerFactory;
    }

    public ILightningClient? Create(string connectionString, Network network, out string? error)
    {
        error = null;

        var keyValues = LightningConnectionStringHelper.ExtractValues(connectionString, out var type);
        if (type != "breez-liquid")
        {
            error = "Invalid connection type. Expected 'breez-liquid'.";
            return null;
        }

        try
        {
            var config = BreezConnectionConfig.Parse(keyValues);
            var logger = _loggerFactory.CreateLogger<BreezLiquidClient>();
            return new BreezLiquidClient(config, _sdkManager, logger, network);
        }
        catch (FormatException ex)
        {
            error = ex.Message;
            return null;
        }
    }
}
