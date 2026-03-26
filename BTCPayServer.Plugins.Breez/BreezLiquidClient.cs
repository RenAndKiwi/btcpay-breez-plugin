using BTCPayServer.Lightning;
using Breez.Sdk.Liquid;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace BTCPayServer.Plugins.Breez;

/// <summary>
/// ILightningClient implementation backed by Breez SDK Liquid.
/// Handles invoice creation, payment, balance, and listening.
/// </summary>
public sealed class BreezLiquidClient : ILightningClient
{
    private readonly BreezConnectionConfig _config;
    private readonly BreezSdkManager _sdkManager;
    private readonly ILogger<BreezLiquidClient> _logger;
    private readonly NBitcoin.Network _network;

    public BreezLiquidClient(
        BreezConnectionConfig config,
        BreezSdkManager sdkManager,
        ILogger<BreezLiquidClient> logger,
        NBitcoin.Network network)
    {
        _config = config;
        _sdkManager = sdkManager;
        _logger = logger;
        _network = network;
    }

    private async Task<BindingLiquidSdk> GetSdkAsync(CancellationToken ct = default)
    {
        return await _sdkManager.GetOrConnectAsync(_config, ct);
    }

    // ──────────────────────────────────────────────
    // Invoice operations
    // ──────────────────────────────────────────────

    public async Task<LightningInvoice> CreateInvoice(LightMoney amount, string description, TimeSpan expiry, CancellationToken cancellation = default)
    {
        return await CreateInvoice(new CreateInvoiceParams(amount, description, expiry), cancellation);
    }

    public async Task<LightningInvoice> CreateInvoice(CreateInvoiceParams req, CancellationToken cancellation = default)
    {
        var sdk = await GetSdkAsync(cancellation);

        var amountSat = (ulong)(req.Amount.MilliSatoshi / 1000);
        var receiveAmount = new ReceiveAmount.Bitcoin(amountSat);

        var prepareReq = new PrepareReceiveRequest(
            paymentMethod: PaymentMethod.Bolt11Invoice,
            amount: receiveAmount
        );

        var prepareResp = sdk.PrepareReceivePayment(prepareReq);

        var receiveReq = new ReceivePaymentRequest(
            prepareResponse: prepareResp,
            description: req.Description,
            descriptionHash: null,
            payerNote: null
        );

        var resp = sdk.ReceivePayment(receiveReq);

        // destination is the BOLT11 invoice string
        var bolt11 = resp.destination;

        // Parse the invoice to get the payment hash for the ID
        string invoiceId;
        try
        {
            var parsed = BreezSdkLiquidMethods.ParseInvoice(bolt11);
            invoiceId = parsed.paymentHash;
        }
        catch
        {
            invoiceId = Guid.NewGuid().ToString();
        }

        return new LightningInvoice
        {
            Id = invoiceId,
            BOLT11 = bolt11,
            Amount = req.Amount,
            ExpiresAt = DateTimeOffset.UtcNow + req.Expiry,
            Status = LightningInvoiceStatus.Unpaid
        };
    }

    public async Task<LightningInvoice?> GetInvoice(string invoiceId, CancellationToken cancellation = default)
    {
        var sdk = await GetSdkAsync(cancellation);

        var getReq = new GetPaymentRequest.PaymentHash(invoiceId);
        var payment = sdk.GetPayment(getReq);

        if (payment == null) return null;

        return MapPaymentToInvoice(payment);
    }

    public async Task<LightningInvoice?> GetInvoice(uint256 paymentHash, CancellationToken cancellation = default)
    {
        return await GetInvoice(paymentHash.ToString(), cancellation);
    }

    public async Task<LightningInvoice[]> ListInvoices(CancellationToken cancellation = default)
    {
        return await ListInvoices(null, cancellation);
    }

    public async Task<LightningInvoice[]> ListInvoices(ListInvoicesParams? request, CancellationToken cancellation = default)
    {
        var sdk = await GetSdkAsync(cancellation);
        var listReq = new ListPaymentsRequest();

        var payments = sdk.ListPayments(listReq);

        var invoices = payments
            .Where(p => p.paymentType == PaymentType.Receive)
            .Select(MapPaymentToInvoice)
            .ToArray();

        if (request?.PendingOnly == true)
        {
            invoices = invoices.Where(i => i.Status == LightningInvoiceStatus.Unpaid).ToArray();
        }

        return invoices;
    }

    public Task CancelInvoice(string invoiceId, CancellationToken cancellation = default)
    {
        throw new NotSupportedException("Breez SDK Liquid does not support invoice cancellation.");
    }

    // ──────────────────────────────────────────────
    // Payment operations
    // ──────────────────────────────────────────────

    public async Task<PayResponse> Pay(string bolt11, CancellationToken cancellation = default)
    {
        return await Pay(bolt11, null, cancellation);
    }

    public async Task<PayResponse> Pay(string bolt11, PayInvoiceParams? payParams, CancellationToken cancellation = default)
    {
        var sdk = await GetSdkAsync(cancellation);

        try
        {
            var prepareReq = new PrepareSendRequest(
                destination: bolt11,
                amount: null, // Use invoice amount
                disableMrh: null,
                paymentTimeoutSec: null
            );

            var prepareResp = sdk.PrepareSendPayment(prepareReq);

            var sendReq = new SendPaymentRequest(
                prepareResponse: prepareResp,
                useAssetFees: null,
                payerNote: null
            );

            var sendResp = sdk.SendPayment(sendReq);
            var payment = sendResp.payment;

            return new PayResponse(PayResult.Ok, new PayDetails
            {
                TotalAmount = new LightMoney((long)(payment.amountSat * 1000), LightMoneyUnit.MilliSatoshi),
                FeeAmount = new LightMoney((long)(payment.feesSat * 1000), LightMoneyUnit.MilliSatoshi),
                Status = LightningPaymentStatus.Complete
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Breez SDK payment failed for bolt11 {Bolt11}", bolt11[..Math.Min(20, bolt11.Length)]);
            return new PayResponse(PayResult.Error, ex.Message);
        }
    }

    public Task<PayResponse> Pay(PayInvoiceParams payParams, CancellationToken cancellation = default)
    {
        throw new NotSupportedException("Breez SDK Liquid requires a bolt11 invoice for payment.");
    }

    public async Task<LightningPayment?> GetPayment(string paymentHash, CancellationToken cancellation = default)
    {
        var sdk = await GetSdkAsync(cancellation);

        var getReq = new GetPaymentRequest.PaymentHash(paymentHash);
        var payment = sdk.GetPayment(getReq);

        if (payment == null) return null;

        return new LightningPayment
        {
            Id = paymentHash,
            Amount = new LightMoney((long)(payment.amountSat * 1000), LightMoneyUnit.MilliSatoshi),
            Fee = new LightMoney((long)(payment.feesSat * 1000), LightMoneyUnit.MilliSatoshi),
            Status = MapPaymentStatus(payment.status),
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(payment.timestamp)
        };
    }

    public async Task<LightningPayment[]> ListPayments(CancellationToken cancellation = default)
    {
        return await ListPayments(null, cancellation);
    }

    public async Task<LightningPayment[]> ListPayments(ListPaymentsParams? request, CancellationToken cancellation = default)
    {
        var sdk = await GetSdkAsync(cancellation);
        var payments = sdk.ListPayments(new ListPaymentsRequest());

        return payments
            .Where(p => p.paymentType == PaymentType.Send)
            .Select(p => new LightningPayment
            {
                Id = p.txId ?? p.destination,
                Amount = new LightMoney((long)(p.amountSat * 1000), LightMoneyUnit.MilliSatoshi),
                Fee = new LightMoney((long)(p.feesSat * 1000), LightMoneyUnit.MilliSatoshi),
                Status = MapPaymentStatus(p.status),
                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(p.timestamp)
            })
            .ToArray();
    }

    // ──────────────────────────────────────────────
    // Info & Balance
    // ──────────────────────────────────────────────

    public async Task<LightningNodeInformation> GetInfo(CancellationToken cancellation = default)
    {
        var sdk = await GetSdkAsync(cancellation);
        var info = sdk.GetInfo();

        return new LightningNodeInformation
        {
            Alias = "Breez SDK Liquid",
            BlockHeight = 0, // Liquid sidechain, not relevant for BTCPay
            Version = "breez-liquid-0.12"
        };
    }

    public async Task<LightningNodeBalance> GetBalance(CancellationToken cancellation = default)
    {
        var sdk = await GetSdkAsync(cancellation);
        var info = sdk.GetInfo();

        var offchain = new OffchainBalance
        {
            Local = new LightMoney((long)(info.walletInfo.balanceSat * 1000), LightMoneyUnit.MilliSatoshi)
        };

        return new LightningNodeBalance(null, offchain);
    }

    public Task<BitcoinAddress> GetDepositAddress(CancellationToken cancellation = default)
    {
        // Breez SDK Liquid can receive on-chain BTC, but returns a Liquid/BIP21 URI.
        // BTCPay expects a Bitcoin address. This is a best-effort fallback.
        throw new NotSupportedException(
            "Breez SDK Liquid uses Liquid addresses for on-chain deposits, not Bitcoin addresses.");
    }

    // ──────────────────────────────────────────────
    // Listening
    // ──────────────────────────────────────────────

    public Task<ILightningInvoiceListener> Listen(CancellationToken cancellation = default)
    {
        return Task.FromResult<ILightningInvoiceListener>(
            new BreezInvoiceListener(_config, _sdkManager, _logger));
    }

    // ──────────────────────────────────────────────
    // Unsupported (nodeless — no channels)
    // ──────────────────────────────────────────────

    public Task<OpenChannelResponse> OpenChannel(OpenChannelRequest openChannelRequest, CancellationToken cancellation = default)
        => throw new NotSupportedException("Breez SDK Liquid is nodeless. No channel operations.");

    public Task<ConnectionResult> ConnectTo(NodeInfo nodeInfo, CancellationToken cancellation = default)
        => throw new NotSupportedException("Breez SDK Liquid is nodeless. No peer connections.");

    public Task<LightningChannel[]> ListChannels(CancellationToken cancellation = default)
        => Task.FromResult(Array.Empty<LightningChannel>());

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static LightningInvoice MapPaymentToInvoice(Payment payment)
    {
        var status = payment.status switch
        {
            PaymentState.Complete => LightningInvoiceStatus.Paid,
            PaymentState.Failed or PaymentState.TimedOut => LightningInvoiceStatus.Expired,
            _ => LightningInvoiceStatus.Unpaid
        };

        return new LightningInvoice
        {
            Id = payment.txId ?? payment.destination,
            Amount = new LightMoney((long)(payment.amountSat * 1000), LightMoneyUnit.MilliSatoshi),
            Status = status,
            PaidAt = status == LightningInvoiceStatus.Paid
                ? DateTimeOffset.FromUnixTimeSeconds(payment.timestamp)
                : null
        };
    }

    private static LightningPaymentStatus MapPaymentStatus(PaymentState state) => state switch
    {
        PaymentState.Complete => LightningPaymentStatus.Complete,
        PaymentState.Failed or PaymentState.TimedOut => LightningPaymentStatus.Failed,
        _ => LightningPaymentStatus.Pending
    };

    public override string ToString() => $"type=breez-liquid";
}
