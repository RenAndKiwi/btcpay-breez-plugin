using BTCPayServer.Lightning;
using Breez.Sdk.Liquid;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Breez;

/// <summary>
/// Implements ILightningInvoiceListener by polling the Breez SDK for payment state changes.
/// BTCPay calls WaitInvoice() in a loop to detect newly paid invoices.
/// </summary>
public sealed class BreezInvoiceListener : ILightningInvoiceListener
{
    private readonly BreezConnectionConfig _config;
    private readonly BreezSdkManager _sdkManager;
    private readonly ILogger _logger;
    private readonly HashSet<string> _knownPaidIds = new();
    private bool _initialized;
    #pragma warning disable CS0414
    private bool _disposed;
    #pragma warning restore CS0414

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    public BreezInvoiceListener(
        BreezConnectionConfig config,
        BreezSdkManager sdkManager,
        ILogger logger)
    {
        _config = config;
        _sdkManager = sdkManager;
        _logger = logger;
    }

    /// <summary>
    /// Blocks until a new invoice is paid, then returns it.
    /// Called in a loop by BTCPay Server.
    /// </summary>
    public async Task<LightningInvoice> WaitInvoice(CancellationToken cancellation)
    {
        var sdk = await _sdkManager.GetOrConnectAsync(_config, cancellation);

        // On first call, snapshot current paid invoices so we don't re-report old ones
        if (!_initialized)
        {
            var existing = sdk.ListPayments(new ListPaymentsRequest());
            foreach (var p in existing.Where(p =>
                         p.paymentType == PaymentType.Receive &&
                         p.status == PaymentState.Complete))
            {
                var id = p.txId ?? p.destination ?? p.ToString();
                _knownPaidIds.Add(id);
            }
            _initialized = true;
        }

        // Poll for new payments
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                var payments = sdk.ListPayments(new ListPaymentsRequest());
                var newlyPaid = payments
                    .Where(p =>
                        p.paymentType == PaymentType.Receive &&
                        p.status == PaymentState.Complete)
                    .FirstOrDefault(p =>
                    {
                        var id = p.txId ?? p.destination ?? p.ToString();
                        return !_knownPaidIds.Contains(id);
                    });

                if (newlyPaid != null)
                {
                    var id = newlyPaid.txId ?? newlyPaid.destination ?? newlyPaid.ToString();
                    _knownPaidIds.Add(id);
                    _logger.LogInformation("New payment detected: {Id}, {Amount} sats",
                        id, newlyPaid.amountSat);

                    return new LightningInvoice
                    {
                        Id = id,
                        Amount = new LightMoney((long)(newlyPaid.amountSat * 1000), LightMoneyUnit.MilliSatoshi),
                        Status = LightningInvoiceStatus.Paid,
                        PaidAt = DateTimeOffset.FromUnixTimeSeconds(newlyPaid.timestamp)
                    };
                }
            }
            catch (Exception ex) when (!cancellation.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Error polling Breez SDK for payments, retrying...");
            }

            await Task.Delay(PollInterval, cancellation);
        }

        throw new OperationCanceledException(cancellation);
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
