"""Build the production Compose environment from GitHub Actions secrets."""

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


SETTINGS = (
    "ADMIN_DOMAIN",
    "ADMIN_PORT",
    "ADMIN__PASSWORDHASH",
    "ADMIN__USERNAME",
    "API_DOMAIN",
    "API_PORT",
    "JWT__AUDIENCE",
    "JWT__ISSUER",
    "JWT__SIGNINGKEY",
    "MAX__APIBASEURL",
    "MAX__BOTTOKEN",
    "MAX__WEBHOOKSECRET",
    "MAX__WEBAPPNAME",
    "MINIO_API_PORT",
    "MINIO_CONSOLE_PORT",
    "MINIO_ROOT_PASSWORD",
    "MINIO_ROOT_USER",
    "MINIO__ACCESSKEY",
    "MINIO__BUCKETNAME",
    "MINIO__ENDPOINT",
    "MINIO__PUBLICBASEURL",
    "MINIO__SECRETKEY",
    "MINIO__USESSL",
    "POSTGRES_DB",
    "POSTGRES_HOST",
    "POSTGRES_PASSWORD",
    "POSTGRES_PORT",
    "POSTGRES_USER",
    "SWAGGER__ENABLED",
    "WEBAPP_DOMAIN",
    "WEBAPP_PORT",
)
settings = {name: required(name) for name in SETTINGS}

domains = [settings[key] for key in ("API_DOMAIN", "ADMIN_DOMAIN", "WEBAPP_DOMAIN")]
if len(set(domains)) != 3 or any(
    not re.fullmatch(r"[a-z0-9]+(?:[.-][a-z0-9]+)*", domain) for domain in domains
):
    raise SystemExit("Set three distinct domain names without schemes or paths")

if len(settings["JWT__SIGNINGKEY"].encode("utf-8")) < 32:
    raise SystemExit("JWT__SIGNINGKEY must contain at least 32 bytes")
if not re.fullmatch(r"[A-Za-z0-9_-]{5,256}", settings["MAX__WEBHOOKSECRET"]):
    raise SystemExit("MAX__WEBHOOKSECRET has an invalid format")

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
