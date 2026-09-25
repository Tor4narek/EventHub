"""Build the production Compose environment from public defaults and CI secrets."""

import os
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / ".env"


def required(name: str) -> str:
    value = os.environ.get(name, "")
    if not value:
        raise SystemExit(f"Missing deployment secret: {name}")
    return value


def quote(value: str) -> str:
    if "\n" in value or "\r" in value:
        raise SystemExit("Environment values must be single-line strings")
    return "'" + value.replace("\\", "\\\\").replace("'", "\\'") + "'"


settings: dict[str, str] = {}
for line in (ROOT / ".env.example").read_text(encoding="utf-8").splitlines():
    if not line or line.startswith("#"):
        continue
    key, separator, value = line.partition("=")
    if separator:
        settings[key] = value

for variable, key in (
    ("API_DOMAIN", "API_DOMAIN"),
    ("ADMIN_DOMAIN", "ADMIN_DOMAIN"),
    ("WEBAPP_DOMAIN", "WEBAPP_DOMAIN"),
    ("ADMIN_USERNAME", "Admin__Username"),
    ("MINIO_ROOT_USER", "MINIO_ROOT_USER"),
    ("MAX_WEBAPP_NAME", "Max__WebAppName"),
):
    if os.environ.get(variable):
        settings[key] = os.environ[variable]

domains = [settings[key] for key in ("API_DOMAIN", "ADMIN_DOMAIN", "WEBAPP_DOMAIN")]
if len(set(domains)) != 3 or any(
    not re.fullmatch(r"[a-z0-9]+(?:[.-][a-z0-9]+)*", domain) for domain in domains
):
    raise SystemExit("Set three distinct domain names without schemes or paths")

settings["POSTGRES_PASSWORD"] = required("POSTGRES_PASSWORD")
settings["Jwt__SigningKey"] = required("JWT_SIGNING_KEY")
settings["Admin__PasswordHash"] = required("ADMIN_PASSWORD_HASH")
settings["Max__BotToken"] = required("MAX_BOT_TOKEN")
settings["Max__WebhookSecret"] = required("MAX_WEBHOOK_SECRET")
settings["MINIO_ROOT_PASSWORD"] = required("MINIO_ROOT_PASSWORD")
settings["Minio__AccessKey"] = settings["MINIO_ROOT_USER"]
settings["Minio__SecretKey"] = settings["MINIO_ROOT_PASSWORD"]
settings["Minio__PublicBaseUrl"] = f"https://{settings['API_DOMAIN']}"

if len(settings["Jwt__SigningKey"].encode("utf-8")) < 32:
    raise SystemExit("JWT_SIGNING_KEY must contain at least 32 bytes")
if not re.fullmatch(r"[A-Za-z0-9_-]{5,256}", settings["Max__WebhookSecret"]):
    raise SystemExit("MAX_WEBHOOK_SECRET has an invalid format")

OUTPUT.parent.mkdir(parents=True, exist_ok=True)
descriptor = os.open(OUTPUT, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
    for key, value in settings.items():
        stream.write(f"{key}={quote(value)}\n")
if os.environ.get("GITHUB_OUTPUT"):
    with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as stream:
        for key in ("API_DOMAIN", "ADMIN_DOMAIN", "WEBAPP_DOMAIN"):
            stream.write(f"{key.lower()}={settings[key]}\n")
print(f"Generated deployment environment with {len(settings)} settings")
