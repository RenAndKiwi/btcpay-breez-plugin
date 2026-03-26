using BTCPayServer.Lightning;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.Breez;

/// <summary>
/// BTCPay Server plugin entry point. Registers Breez SDK Liquid as a Lightning backend.
/// </summary>
public class BreezPlugin
{
    /// <summary>
    /// Called by BTCPay to register services.
    /// </summary>
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<BreezSdkManager>();
        services.AddSingleton<ILightningConnectionStringHandler, BreezConnectionHandler>();
    }
}
