# FAQ

## Did you modify Miningcore code to validate or pay NOVA blocks?
No. Block validation and payout use the standard Ethereum flow in Miningcore. If blocks stay in NEW/Pending, it is almost always a configuration or RPC mismatch (see `operator-kit/troubleshooting.md`).

## What are the correct chainId/networkId values?
Mainnet uses chainId/networkId `121525` (hex `0x1dab5`).

## Is a custom coin template required?
No. This fork already includes `ethernova` in `src/Miningcore/coins.json`. Use `coin: "ethernova"` with `family: ethereum` and `algorithm: Ethash`.

## What stratum login formats are supported?
- `0xWALLET.worker`
- `0xWALLET/worker`
- `0xWALLET:worker`
- `username=0xWALLET`, `password=worker`
If no worker label is provided, the worker name defaults to `default` after the first share.

## What is the minimum payout?
Default minimum payout is `1.0` NOVA. Adjust `pools[].paymentProcessing.minimumPayment` in your config.

## Do I need ZMQ?
No. ZMQ (`btStream`) is optional. Miningcore works with standard JSON-RPC only.
