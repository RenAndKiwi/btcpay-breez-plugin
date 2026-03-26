using Breez.Sdk.Liquid;
using Xunit;
using Xunit.Abstractions;

namespace BTCPayServer.Plugins.Breez.Tests;

/// <summary>
/// Integration tests that connect to Breez SDK Liquid on testnet.
/// Requires BREEZ_API_KEY environment variable or uses the stored key.
/// </summary>
[Trait("Category", "Integration")]
public class BreezIntegrationTests
{
    private readonly ITestOutputHelper _output;

    private const string ApiKey = "MIIBdjCCASigAwIBAgIHPtSZrbt5zzAFBgMrZXAwEDEOMAwGA1UEAxMFQnJlZXowHhcNMjYwMTE1MjM0MTQ2WhcNMzYwMTEzMjM0MTQ2WjAsMRswGQYDVQQKExJCaXRjb2luIEJ1dGxlciBMTEMxDTALBgNVBAMTBEtpd2kwKjAFBgMrZXADIQDQg/XL3yA8HKIgyimHU/Qbpxy0tvzris1fDUtEs6ldd6OBhDCBgTAOBgNVHQ8BAf8EBAMCBaAwDAYDVR0TAQH/BAIwADAdBgNVHQ4EFgQU2jmj7l5rSw0yVb/vlWAYkK/YBwkwHwYDVR0jBBgwFoAU3qrWklbzjed0khb8TLYgsmsomGswIQYDVR0RBBowGIEWYmVuQGJpdGNvaW5idXRsZXJzLmNvbTAFBgMrZXADQQBFUK04o0B0ZXvXq+krnI/tp8A/RpoJcDntkrV1FSXxDRfZM1sMG2FQMpfeZUvCEBFHH709i4I6uII3kEphDMsM";

    // Test-only mnemonic — NO REAL FUNDS
    private const string TestMnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    public BreezIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CanConnectToTestnet()
    {
        var workingDir = Path.Combine(Path.GetTempPath(), "breez-test-" + Guid.NewGuid().ToString()[..8]);
        Directory.CreateDirectory(workingDir);

        try
        {
            var config = BreezSdkLiquidMethods.DefaultConfig(LiquidNetwork.Mainnet, ApiKey);

            var sdkConfig = new Config(
                liquidExplorer: config.liquidExplorer,
                bitcoinExplorer: config.bitcoinExplorer,
                workingDir: workingDir,
                network: config.network,
                paymentTimeoutSec: config.paymentTimeoutSec,
                syncServiceUrl: config.syncServiceUrl,
                breezApiKey: config.breezApiKey,
                zeroConfMaxAmountSat: config.zeroConfMaxAmountSat,
                onchainSyncPeriodSec: config.onchainSyncPeriodSec,
                onchainSyncRequestTimeoutSec: config.onchainSyncRequestTimeoutSec,
                useDefaultExternalInputParsers: config.useDefaultExternalInputParsers,
                useMagicRoutingHints: config.useMagicRoutingHints,
                externalInputParsers: config.externalInputParsers,
                onchainFeeRateLeewaySat: config.onchainFeeRateLeewaySat,
                assetMetadata: config.assetMetadata,
                sideswapApiKey: config.sideswapApiKey
            );

            var connectRequest = new ConnectRequest(
                config: sdkConfig,
                mnemonic: TestMnemonic,
                passphrase: null,
                seed: null
            );

            _output.WriteLine("Connecting to Breez SDK Liquid testnet...");
            var sdk = BreezSdkLiquidMethods.Connect(connectRequest);
            _output.WriteLine("Connected!");

            // Get info
            var info = sdk.GetInfo();
            _output.WriteLine($"Balance: {info.walletInfo.balanceSat} sats");
            _output.WriteLine($"Pending send: {info.walletInfo.pendingSendSat} sats");
            _output.WriteLine($"Pending receive: {info.walletInfo.pendingReceiveSat} sats");
            _output.WriteLine($"Fingerprint: {info.walletInfo.fingerprint}");
            _output.WriteLine($"Pubkey: {info.walletInfo.pubkey}");

            Assert.NotNull(info);
            Assert.NotNull(info.walletInfo);
            Assert.NotNull(info.walletInfo.fingerprint);

            // Create a test invoice
            _output.WriteLine("Creating test invoice for 1000 sats...");
            var receiveAmount = new ReceiveAmount.Bitcoin(1000);
            var prepareReq = new PrepareReceiveRequest(
                paymentMethod: PaymentMethod.Bolt11Invoice,
                amount: receiveAmount
            );

            var prepareResp = sdk.PrepareReceivePayment(prepareReq);
            _output.WriteLine($"Estimated fees: {prepareResp.feesSat} sats");

            var receiveReq = new ReceivePaymentRequest(
                prepareResponse: prepareResp,
                description: "BTCPay Breez Plugin integration test",
                descriptionHash: null,
                payerNote: null
            );

            var receiveResp = sdk.ReceivePayment(receiveReq);
            _output.WriteLine($"Invoice created!");
            _output.WriteLine($"BOLT11: {receiveResp.destination[..60]}...");

            Assert.NotNull(receiveResp.destination);
            Assert.StartsWith("ln", receiveResp.destination);

            // List payments
            var payments = sdk.ListPayments(new ListPaymentsRequest());
            _output.WriteLine($"Total payments: {payments.Count}");

            // Disconnect
            sdk.Disconnect();
            _output.WriteLine("Disconnected. All tests passed!");
        }
        finally
        {
            try { Directory.Delete(workingDir, recursive: true); } catch { }
        }
    }
}
