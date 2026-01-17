# Operator Kit Deploy (Ubuntu 22.04/24.04)

This deployment kit captures the current NOVA production setup (Miningcore + Nginx + Postgres + WebUI) in a repeatable, upstream-friendly way. It is designed for a fresh Ubuntu VPS.

Source of truth
- Miningcore code: this repo (EthernovaDev/mining-core)
- WebUI: upstream https://github.com/minernl/Miningcore.WebUI + patches in operator-kit/deploy/patches
- Node: Ethernova CoreGeth (ethernova-coregeth) https://github.com/EthernovaDev/ethernova-coregeth
- Secrets: keep in /etc/miningcore/*.pass (chmod 600). Never commit secrets.

What you get
- HTTPS WebUI at your domain
- /api proxied to 127.0.0.1:4000
- Stratum on 0.0.0.0:4073 (and 4074 if enabled)
- Postgres configured for Miningcore
- Optional local-only Ethernova node RPC (127.0.0.1:8545)
- WebUI hotfix for loadStatsChart API retries

Required ports
- 80/tcp and 443/tcp (public)
- 4073/tcp and 4074/tcp (public)
- 4000/tcp (local-only; do not open publicly)
- 8545/tcp (local-only for node RPC)

Firewall (example with ufw)
- ufw allow 22/tcp
- ufw allow 80/tcp
- ufw allow 443/tcp
- ufw allow 4073/tcp
- ufw allow 4074/tcp

DNS + certbot flow
1) Create an A record for your domain, ex: pool.ethnova.net -> YOUR_SERVER_IP
2) Run the nginx + certbot script after DNS is live


Quick start (scripts)
Run these from the repo root after cloning this repo to the server:

1) Install dependencies
  sudo ./operator-kit/deploy/scripts/00-install-deps.sh

2) Build Miningcore
  sudo ./operator-kit/deploy/scripts/20-build-miningcore.sh

3) Copy configs
  sudo mkdir -p /etc/miningcore /etc/ethernova
  sudo cp operator-kit/deploy/configs/miningcore-config-nova-pool.json /etc/miningcore/config.json
  sudo cp operator-kit/deploy/configs/miningcore-config-nova-solo.json /etc/miningcore/config-solo.json
  sudo cp operator-kit/deploy/configs/coins-custom.json /etc/miningcore/coins-custom.json
  sudo cp operator-kit/deploy/configs/ethernova.toml /etc/ethernova/ethernova.toml

4) Edit config values
  sudo nano /etc/miningcore/config.json
  - pool wallet address
  - rewardRecipients address and percent
  - daemon RPC host/port
  - Postgres password

5) Setup Postgres (creates DB/user and /etc/miningcore/db.pass)
  sudo ./operator-kit/deploy/scripts/10-setup-postgres.sh

6) Deploy WebUI + apply patches
  sudo ./operator-kit/deploy/scripts/30-deploy-webui.sh

7) Configure Nginx + Certbot
  export DOMAIN=pool.example.com
  export EMAIL=admin@example.com
  sudo ./operator-kit/deploy/scripts/40-configure-nginx-certbot.sh

8) Enable services
  sudo ./operator-kit/deploy/scripts/50-enable-services.sh

9) Verify
  sudo ./operator-kit/deploy/scripts/60-verify.sh


Configs overview
- Miningcore pool config template: operator-kit/deploy/configs/miningcore-config-nova-pool.json
- Miningcore solo config template: operator-kit/deploy/configs/miningcore-config-nova-solo.json
- Coin template override: operator-kit/deploy/configs/coins-custom.json
- Nginx vhost template: operator-kit/deploy/configs/nginx-pool.ethnova.net.conf
- systemd units: operator-kit/deploy/configs/systemd/*.service
- Node config: operator-kit/deploy/configs/ethernova.toml

WebUI hotfix
- Patch file: operator-kit/deploy/patches/webui-loadStatsChart-retry.patch
- Apply manually:
  cd /var/www/pool
  git apply /path/to/operator-kit/deploy/patches/webui-loadStatsChart-retry.patch
- Or use the helper script:
  sudo ./operator-kit/deploy/scripts/apply-webui-patches.sh /var/www/pool

How to verify (manual)
- Services:
  systemctl status miningcore nginx postgresql --no-pager
- Ports:
  ss -ltnp | egrep ':80|:443|:4000|:4073|:4074'
- API (local):
  curl -s http://127.0.0.1:4000/api/pools | head
- HTTPS:
  curl -I https://your-domain/
  curl -s https://your-domain/api/pools | head

Troubleshooting
- Stratum not listening until node is synced:
  - CoreGeth can take time to sync. Miningcore will not accept stratum shares if RPC is not ready.
- API no response:
  - Check miningcore logs and API bind address (127.0.0.1:4000).
  - Confirm nginx is proxying /api to 127.0.0.1:4000.
- WebUI shows stale charts:
  - The hotfix uses stale-while-revalidate. If API is down, last known chart data is kept.
- Postgres errors:
  - Confirm /etc/miningcore/db.pass exists and matches the config password.
  - Check pg_hba.conf if local auth fails.

Notes
- API should stay on 127.0.0.1 and only be exposed via nginx /api.
- Do not commit secrets or server-specific values.
