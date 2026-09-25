# Deployment on Ubuntu

The root `compose.yaml` starts PostgreSQL, MinIO, the .NET API with MAX bot and
scheduler, the admin frontend, and the MAX WebApp. The `production` profile
also starts Caddy for HTTPS on ports 80 and 443. Only Caddy is publicly bound;
the application and data ports bind to `127.0.0.1` on the server.

## 1. Prepare the server

Use Ubuntu 24.04 or 22.04 with a public IP. Run these commands on the server:

```bash
sudo apt update
sudo apt install -y ca-certificates curl rsync ufw
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc
sudo tee /etc/apt/sources.list.d/docker.sources >/dev/null <<EOF
Types: deb
URIs: https://download.docker.com/linux/ubuntu
Suites: $(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}")
Components: stable
Architectures: $(dpkg --print-architecture)
Signed-By: /etc/apt/keyrings/docker.asc
EOF
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo adduser --disabled-password --gecos '' deploy
sudo usermod -aG docker deploy
sudo install -d -m 0755 -o deploy -g deploy /opt/eventhub/app
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```

Add your deployment SSH public key to `/home/deploy/.ssh/authorized_keys` with
directory mode `700`, file mode `600`, and owner `deploy:deploy`. Confirm a new
SSH login as `deploy` and `docker compose version` under that account before
using GitHub Actions. Docker group membership takes effect on a new login.

## 2. DNS and environment

Create three DNS `A` records pointing to `185.185.68.210`: `api.eventhub.faberlab.tech`,
`admin.eventhub.faberlab.tech`, and `eventhub.faberlab.tech`. Fill the root
`.env` from `.env.example`:

- `API_DOMAIN`, `ADMIN_DOMAIN`, `WEBAPP_DOMAIN`: bare domain names, without
  `https://` or path.
- `Minio__PublicBaseUrl=https://<API_DOMAIN>`: images are served through the
  API, while MinIO itself stays private.
- `Max__WebhookSecret`: a new random value accepted by MAX; for example,
  `openssl rand -hex 32`.
- `Max__WebAppName`: the name of the WebApp connected to the bot in MAX. Leave
  it empty until the WebApp is registered; fill it and update the `ENV_FILE`
  GitHub secret before enabling the bot's WebApp button.
- PostgreSQL, MinIO, JWT, admin password hash, and bot token settings must be
  filled. `Minio__AccessKey` must match `MINIO_ROOT_USER`, and
  `Minio__SecretKey` must match `MINIO_ROOT_PASSWORD`.

The user-facing frontends call `/api` on their own origin, and their Nginx
containers proxy it to the API on the Compose network. `VITE_API_BASE_URL` is
therefore not needed for this deployment.

## 3. GitHub Actions secrets

In the new monorepo, set these repository secrets:

| Secret | Value |
| --- | --- |
| `DEPLOY_HOST` | Public IP or SSH hostname of the server |
| `DEPLOY_USER` | `deploy` |
| `DEPLOY_SSH_KEY` | Private key for the `deploy` account |
| `DEPLOY_KNOWN_HOSTS` | Verified SSH host-key line for the server |
| `ENV_FILE` | Entire contents of the root production `.env` |

Set `DEPLOY_HOST` to `185.185.68.210`. The workflow checks that all three
domains resolve to this IP before copying files to the server. Verify DNS with
`nslookup api.eventhub.faberlab.tech 1.1.1.1` and the other two domains.

Get a host-key line with `ssh-keyscan -H 185.185.68.210`, and compare its
fingerprint against `ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub` run on the
server. Do not paste private keys or `.env` into Git. The workflow validates
required settings, copies the source over SSH, builds all images on the server,
and starts the production profile after a push to `main` or a manual run.

## 4. HTTPS and MAX

With DNS records in place and ports 80/443 reachable, Caddy obtains and renews
certificates automatically. No manual Certbot step is needed. Check:

```bash
curl -I https://<API_DOMAIN>/api/max/health
curl -I https://<ADMIN_DOMAIN>/
curl -I https://<WEBAPP_DOMAIN>/health
```

Register the MAX webhook as `https://<API_DOMAIN>/api/max/webhook` and use the
same `Max__WebhookSecret` when creating the subscription. Set the MAX WebApp URL
to `https://<WEBAPP_DOMAIN>/`. The API applies existing EF migrations when it
starts. Keep one API replica because the bot scheduler runs inside it.

## Operations

On the server, run commands from `/opt/eventhub/app`:

```bash
docker compose --profile production ps
docker compose --profile production logs -f --tail=100
docker compose --profile production restart
docker compose --profile production down
```

The named volumes contain PostgreSQL data, MinIO images, and Caddy certificates.
`down` preserves them; `down -v` removes them. Back up PostgreSQL and MinIO
volumes outside this VPS. The GitHub workflow does not prune Docker volumes.
