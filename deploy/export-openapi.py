"""Build the API and export its current OpenAPI document without a database."""

import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
from urllib.error import URLError
from urllib.request import urlopen


ROOT = Path(__file__).resolve().parent.parent
PROJECT = ROOT / "EventHubAPI" / "EventHubAPI" / "EventHubAPI.csproj"
ASSEMBLY = ROOT / "EventHubAPI" / "EventHubAPI" / "bin" / "Debug" / "net10.0" / "EventHubAPI.dll"
OUTPUT = ROOT / "openapi.json"


def main() -> None:
    subprocess.run(
        ["dotnet", "build", str(PROJECT), "-m:1", "/p:UseSharedCompilation=false", "--verbosity", "quiet"],
        cwd=ROOT,
        check=True,
    )

    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        port = listener.getsockname()[1]

    inherited = {"PATH", "SYSTEMROOT", "TEMP", "TMP", "DOTNET_ROOT", "HOME", "USERPROFILE", "APPDATA", "LOCALAPPDATA", "NUGET_PACKAGES"}
    environment = {key: value for key, value in os.environ.items() if key.upper() in inherited}
    environment.update({
        "ASPNETCORE_ENVIRONMENT": "OpenApiExport",
        "ASPNETCORE_URLS": f"http://127.0.0.1:{port}",
        "Jwt__SigningKey": "openapi-export-only-key-0123456789abcdef",
        "Max__BotToken": "openapi-export",
        "POSTGRES_HOST": "127.0.0.1",
        "POSTGRES_PORT": "5432",
        "POSTGRES_DB": "openapi",
        "POSTGRES_USER": "openapi",
        "POSTGRES_PASSWORD": "openapi",
    })

    with tempfile.TemporaryDirectory(prefix="eventhub-openapi-") as temporary_dir:
        log_path = Path(temporary_dir) / "api.log"
        with log_path.open("w+", encoding="utf-8") as log:
            process = subprocess.Popen(
                ["dotnet", str(ASSEMBLY)],
                cwd=temporary_dir,
                env=environment,
                stdout=log,
                stderr=subprocess.STDOUT,
            )
            try:
                deadline = time.monotonic() + 30
                while time.monotonic() < deadline:
                    if process.poll() is not None:
                        break
                    try:
                        with urlopen(f"http://127.0.0.1:{port}/openapi/v1.json", timeout=2) as response:
                            document = json.load(response)
                        if not str(document.get("openapi", "")).startswith(("3.0.", "3.1.")):
                            raise RuntimeError("API returned an unsupported OpenAPI version")
                        OUTPUT.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
                        print(f"Exported {OUTPUT}")
                        return
                    except URLError:
                        time.sleep(0.2)
                log.flush()
                raise RuntimeError(f"OpenAPI endpoint did not start:\n{log_path.read_text(encoding='utf-8')}")
            finally:
                if process.poll() is None:
                    process.terminate()
                    try:
                        process.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait()


if __name__ == "__main__":
    main()
