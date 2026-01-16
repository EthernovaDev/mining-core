# Troubleshooting

## Blocks stuck in NEW/Pending

### Checklist (most common causes)
1) Wrong RPC or wrong chainId/networkId
2) Payment processing disabled
3) Confirmations/maturation set too high
4) Pool wallet mismatch (coinbase/etherbase or extraData)
5) Time drift or NTP issues
6) Database connectivity errors
7) Block confirmation job errors in Miningcore logs

### Commands

Check Miningcore service:
```
systemctl status miningcore --no-pager
journalctl -u miningcore -n 200 --no-pager
```

Check Postgres service:
```
systemctl status postgresql --no-pager
```

Check node service (adjust name if different):
```
systemctl status ethernova --no-pager
journalctl -u ethernova -n 200 --no-pager
```

Verify chainId/networkId and tip height:
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
- `eth_chainId` -> `0x1dab5`
- `net_version` -> `121525`

Verify required RPC methods:
```
curl -s -H 'Content-Type: application/json' \
  --data '{"jsonrpc":"2.0","id":1,"method":"eth_getBlockByNumber","params":["latest", true]}' \
  http://127.0.0.1:8545

curl -s -H 'Content-Type: application/json' \
  --data '{"jsonrpc":"2.0","id":1,"method":"eth_getBlockByHash","params":["0x<block-hash>", true]}' \
  http://127.0.0.1:8545

curl -s -H 'Content-Type: application/json' \
  --data '{"jsonrpc":"2.0","id":1,"method":"eth_getTransactionReceipt","params":["0x<tx-hash>"]}' \
  http://127.0.0.1:8545
```

Confirm pool wallet alignment (coinbase/etherbase):
```
curl -s -H 'Content-Type: application/json' \
  --data '{"jsonrpc":"2.0","id":1,"method":"eth_coinbase","params":[]}' \
  http://127.0.0.1:8545
```
If you enforce custom `extraData` on the node or pool, ensure it matches your pool config.

Check confirmations and payment settings:
- `pools[].blockConfirmations` (default 16)
- `paymentProcessing.enabled` at cluster and pool levels
- `pools[].paymentProcessing.minimumPayment` (default 1.0 NOVA)
- `coin: \"ethernova\"` and `chainTypeOverride: \"Ethereum\"` in the pool config

Check time drift:
```
timedatectl status
```

Check Postgres connectivity:
```
pg_isready -h 127.0.0.1 -p 5432 -U miningcore
```

Useful Miningcore log filters:
```
journalctl -u miningcore -n 300 --no-pager | rg -i "payment|confirm|block|payout|error"
```

## Payments not sent

- Ensure the pool wallet is unlocked if your node requires `personal_unlockAccount`.
- Confirm `paymentProcessing.enabled` is `true` and `interval` is sane.
- Make sure the RPC exposes `eth_sendTransaction` (or use a signer if configured).

## Workers show as default

Supported worker formats:
- `0xWALLET.worker`
- `0xWALLET/worker`
- `0xWALLET:worker`
- `username=0xWALLET`, `password=worker` (ASIC compatibility)

Notes:
- Worker keys appear only after the miner submits at least one share.
- Empty or placeholder passwords (`x`, `0`, `-`) map to `default`.

## API/UI shows no workers

- Confirm `/api/pools/<poolId>/miners/<address>` returns `performance.workers`.
- If `performance` is empty, the miner has not submitted any shares yet.
