# BTCPay Server Breez Plugin

A BTCPay Server plugin that adds **Breez SDK Liquid** as a Lightning backend. Accept Lightning payments without running a node.

No channels. No liquidity management. No infrastructure headache.

## What This Does

This plugin implements BTCPay Server's `ILightningClient` interface using the [Breez SDK (Liquid implementation)](https://sdk-doc-liquid.breez.technology/). When a customer pays a Lightning invoice through your BTCPay checkout, the Breez SDK handles everything: invoice creation, payment routing via submarine swaps, and settlement to your Liquid wallet.

**You provide a mnemonic and a Breez API key. That's it.**

## How It Works

```
Customer → BTCPay Checkout → Breez SDK → Lightning Network → Your Wallet
```

Under the hood, the Breez SDK Liquid uses submarine swaps and reverse submarine swaps to move funds between Lightning and the Liquid sidechain. Your balance is held as L-BTC (Liquid Bitcoin), which you control via your mnemonic.

## Connection String

Configure in BTCPay Server under **Lightning → Settings → Change connection → Use custom node**:

```
type=breez-liquid;api-key=YOUR_BREEZ_API_KEY;mnemonic=YOUR_12_WORD_MNEMONIC
```

### Optional Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `network` | `mainnet` | Use `testnet` for development |
| `working-dir` | auto | Custom directory for SDK state files |

## Getting a Breez API Key

1. Go to [breez.technology/request-api-key](https://breez.technology/request-api-key/)
2. Fill out the form (it's free)
3. You'll receive a Liquid SDK API key via email

## Supported Operations

| Operation | Status |
|-----------|--------|
| Create invoice (BOLT11) | ✅ |
| Receive Lightning payment | ✅ |
| Send Lightning payment | ✅ |
| Check balance | ✅ |
| List invoices | ✅ |
| List payments | ✅ |
| Get node info | ✅ |
| Listen for payments | ✅ (polling) |
| Open/close channels | N/A (nodeless) |
| Peer connections | N/A (nodeless) |

## Trust Model

**Read this before using in production.**

- Your **mnemonic is stored in BTCPay's database** as part of the connection string. If the database is compromised, the attacker gets your wallet. This is the same trust model as running a Lightning node on the same machine.
- Your **funds are held as L-BTC** on the Liquid sidechain, a federated sidechain. This is NOT the same as holding BTC in Lightning channels. The Liquid federation is a set of known functionaries.
- **Breez's LSP handles Lightning routing.** If Breez's infrastructure goes down, payments won't process until they recover. You can always withdraw L-BTC on-chain.

For merchants who need maximum sovereignty, run your own Lightning node. This plugin is for merchants who want Lightning payments without the operational burden.

## Fees

Fees are charged by the Breez swap service, not by this plugin:

- **Sending (Lightning):** ~0.1% + ~53 sats in transaction fees
- **Receiving (Lightning):** ~0.25% + ~47 sats in transaction fees
- **Breez SDK:** Free for developers

See [Breez SDK fee documentation](https://sdk-doc-liquid.breez.technology/guide/base_fees.html) for current rates.

## Installation

### From BTCPay Plugin Builder (coming soon)

Once published, install directly from **Server Settings → Manage Plugins**.

### Manual / Development

1. Clone this repo alongside your BTCPay Server fork:
   ```
   git clone https://github.com/kiwihodl/btcpay-breez-plugin.git
   ```

2. Add to your BTCPay solution:
   ```
   cd btcpayserver
   dotnet sln add ../btcpay-breez-plugin/BTCPayServer.Plugins.Breez -s Plugins
   ```

3. Build:
   ```
   cd BTCPayServer.Plugins.Breez
   dotnet build
   ```

4. Run tests:
   ```
   cd BTCPayServer.Plugins.Breez.Tests
   dotnet test
   ```

## Development

### Prerequisites

- .NET 8.0+
- BTCPay Server source (as submodule or adjacent clone)
- Breez API key (request one for free)

### Project Structure

```
BTCPayServer.Plugins.Breez/
├── BreezPlugin.cs              # Plugin entry, registers services
├── BreezLiquidClient.cs        # ILightningClient implementation
├── BreezConnectionHandler.cs   # Connection string parser
├── BreezConnectionConfig.cs    # Configuration model
├── BreezSdkManager.cs          # SDK lifecycle management
├── BreezInvoiceListener.cs     # Payment polling for BTCPay
└── BTCPayServer.Plugins.Breez.csproj
```

## Built By

[Bitcoin Butlers](https://bitcoinbutlers.com) — helping people hold their own keys.

## License

MIT
