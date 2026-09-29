"""Create or refresh the local API fixture using a test administrator account."""

import json
import os
from datetime import datetime, timedelta, timezone
from pathlib import Path
from urllib.parse import urlencode
from urllib.request import Request, urlopen


BASE_URL = os.environ.get("EVENTHUB_TEST_API_URL", "http://localhost:8456").rstrip("/")
USERNAME = os.environ.get("EVENTHUB_TEST_ADMIN_USERNAME", "")
PASSWORD = os.environ.get("EVENTHUB_TEST_ADMIN_PASSWORD", "")
SCENARIO = json.loads((Path(__file__).with_name("scenario.json")).read_text(encoding="utf-8"))


def call(method, path, body=None, token=None):
    headers = {"Accept": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    data = None
    if body is not None:
        headers["Content-Type"] = "application/json"
        data = json.dumps(body, ensure_ascii=False).encode("utf-8")
    with urlopen(Request(BASE_URL + path, data=data, headers=headers, method=method), timeout=15) as response:
        payload = response.read()
        return json.loads(payload) if payload else None


def main():
    if not USERNAME or not PASSWORD:
        raise SystemExit("Set EVENTHUB_TEST_ADMIN_USERNAME and EVENTHUB_TEST_ADMIN_PASSWORD")

    token = call("POST", "/api/admin/auth/login", {"username": USERNAME, "password": PASSWORD})["accessToken"]
    tag_data = SCENARIO["tag"]
    tags = call("GET", "/api/tags")
    tag = next((item for item in tags if item["name"] == tag_data["name"]), None)
    if tag is None:
        tag = call("POST", "/api/admin/tags", tag_data, token)

    event_data = SCENARIO["event"]
    now = datetime.now(timezone.utc)
    event = {
        "title": event_data["title"],
        "description": event_data["description"],
        "location": event_data["location"],
        "source": event_data["source"],
        "eventDateTime": (now + timedelta(days=event_data["startAfterDays"])).isoformat(),
        "deadline": (now + timedelta(days=event_data["deadlineAfterDays"])).isoformat(),
        "tagIds": [tag["id"]],
        "mainImg": None,
    }
    query = urlencode({"Page": 1, "PageSize": 100, "Search": event_data["title"]})
    existing = call("GET", f"/api/admin/events?{query}", token=token)["items"]
    match = next((item for item in existing if item["title"] == event_data["title"]), None)
    if match:
        item = call("PUT", f"/api/admin/events/{match['id']}", event, token)
    else:
        item = call("POST", "/api/admin/events", event, token)
    if not item["tagsConfirmed"]:
        call("PUT", f"/api/admin/events/{item['id']}/tags", {"tagIds": [tag["id"]]}, token)
    call("POST", f"/api/admin/events/{item['id']}/publish", token=token)
    print(f"Test event: {item['id']} | tag: {tag['id']}")


if __name__ == "__main__":
    main()
