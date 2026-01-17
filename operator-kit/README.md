# Ethernova (NOVA) Pool Operator Kit for Miningcore

This kit gives you a clean, copy-paste setup for running a NOVA pool (PPLNS/PROP) or SOLO on Miningcore.

Deployment kit (Ubuntu 22.04/24.04): operator-kit/deploy/README-DEPLOY.md

## Versions
- Ethernova CoreGeth v1.2.7+
- Miningcore: this fork (EthernovaDev/mining-core). Record the exact commit you deploy with `git rev-parse HEAD`.

## A) Prereqs

### Miningcore build/runtime
- .NET 6 SDK/runtime
- build tools: `cmake`, `build-essential`
- libs: `libssl-dev`, `libboost-all-dev`, `libsodium-dev`, `libzmq5-dev`, `pkg-config`

### Data stores
- PostgreSQL is required (Miningcore persistence layer).
- Redis is optional and only needed if you enable the cluster message bus or share relay features.

### Ethernova node
- Ethernova CoreGeth v1.2.7+
- JSON-RPC enabled and reachable
- chainId/networkId: 121525

RPC methods required by Miningcore:
- `eth_chainId`, `net_version`, `eth_blockNumber`
- `eth_getBlockByNumber`, `eth_getBlockByHash`, `eth_getTransactionReceipt`

## Required ports
- Stratum: TCP (example 4073 for pool, 4074 for solo)
- Miningcore API: TCP 4000 (HTTP)
- CoreGeth JSON-RPC: TCP 8545
- WebSocket: TCP 8546 (optional)
- ZMQ: TCP 5555 (optional, only if you enable `btStream`)

## B) Quick Start (POOL mode)

1) Configure CoreGeth (example in `operator-kit/node-ethernova-example.conf`).

2) Copy the example config:
```
cp operator-kit/miningcore-config-nova-pool.json /etc/miningcore/config.json
```

3) Ensure the NOVA coin template is available:
- This fork already includes `ethernova` in `src/Miningcore/coins.json`.
- Recommended: copy it to `/etc/miningcore/coins.json`:
```
cp /path/to/miningcore/src/Miningcore/coins.json /etc/miningcore/coins.json
```
- Miningcore loads coin templates from `coinTemplates` in `config.json`. Use:
  - `"coinTemplates": ["/etc/miningcore/coins.json"]`

4) Edit `/etc/miningcore/config.json`:
- `pools[].id`: `nova1`
- `pools[].address`: `0xYOUR_POOL_WALLET_ADDRESS`
- `rewardRecipients[0].address`: fee wallet placeholder
- `daemons[0].host` / `port`: CoreGeth RPC endpoint
- `paymentProcessing.minimumPayment`: default `1.0`

5) Start Miningcore:
```
/opt/miningcore/build/Miningcore -c /etc/miningcore/config.json
```

6) Point miners at your Stratum endpoint:
- Example: `stratum+tcp://POOL_HOST:4073`

To switch to PROP, set `pools[].paymentProcessing.payoutScheme` to `PROP`.

## C) Quick Start (SOLO mode)

1) Copy the SOLO example config:
```
cp operator-kit/miningcore-config-nova-solo.json /etc/miningcore/config.json
```

2) Confirm the pool id and port:
- `pools[].id`: `nova1-solo`
- Stratum port: `4074`

3) Start Miningcore:
```
/opt/miningcore/build/Miningcore -c /etc/miningcore/config.json
```

## D) Verify it is on the right chain

```
curl -s -H 'Content-Type: application/json' \
  --data '{"jsonrpc":"2.0","id":1,"method":"eth_chainId","params":[]}' \
  http://127.0.0.1:8545

curl -s -H 'Content-Type: application/json' \
  --data '{"jsonrpc":"2.0","id":1,"method":"net_version","params":[]}' \
  http://127.0.0.1:8545

curl -s -H 'Content-Type: application/json' \
  --data '{"jsonrpc":"2.0","id":1,"method":"eth_blockNumber","params":[]}' \
  http://127.0.0.1:8545
```
Expected:
- `eth_chainId` -> `0x1dab5` (121525)
- `net_version` -> `121525`
- `eth_blockNumber` increases over time

## Config highlights (exact fields)
- `coin`: `ethernova`
- `chainTypeOverride`: `Ethereum`
- `blockConfirmations`: `16` (safe range 16-64; higher reduces orphan risk but delays payouts)
- `paymentProcessing.minimumPayment`: `1.0` (default)
- `pools[].address`: pool payout wallet

## E) Common pitfalls
- Blocks stuck in NEW/Pending: RPC mismatch, payment processor disabled, or confirmations misconfigured.
- No payouts: RPC must allow transaction submission depending on payout mode (sendRawTx, unlocked wallet, or external signer). The `personal` module is only required for unlocked-account mode.
- Workers show as `default`: miner never submitted a share yet, or the worker label is missing.

## F) Monitoring
- Logs: stdout or configured `logFile` path in `config.json`.
- Useful grep patterns:
  - `payment` (payment processor loop)
  - `block` + `confirm` (block confirmation loop)
  - `Share accepted` (stratum health)

## G) Upgrade notes
- Use Ethernova CoreGeth v1.2.7+.

## Worker name behavior
Supported formats (no special config required):
- `0xWALLET.worker1`
- `0xWALLET/worker1`
- `0xWALLET:worker1`
- `username=0xWALLET`, `password=worker1` (ASIC compatibility)

Note: worker keys appear only after at least one share is submitted.

## NOVA specifics (coin template)
- NOVA uses the standard Ethereum/Ethash family in Miningcore.
- Algorithm: Ethash.
- The `ethernova` template is already in `src/Miningcore/coins.json` in this fork.
- No custom code changes are required for block validation or payout; standard Ethereum flow applies.

## How Miningcore confirms blocks
- Block candidates are recorded when a valid share is found at the current height.
- Miningcore queries the daemon for the block hash and receipts via standard Ethereum RPC calls.
- After `blockConfirmations` is reached (default 16), blocks move to Confirmed and balances are credited.
- The payment processor then runs according to `paymentProcessing` settings.

See `operator-kit/troubleshooting.md` and `operator-kit/FAQ.md` for more details.
