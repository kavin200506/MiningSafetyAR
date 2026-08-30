#!/usr/bin/env python3
"""
Auto-create Firestore (Native) database for minesafetyar.
Uses service account at Assets/minesafetyar-firebase-adminsdk-fbsvc-8714f6eb7f.json
If gcloud/firebase CLI not available, uses google-auth + requests to call Admin API.
"""
import json
import os
import sys
import time
import pathlib

SERVICE_ACCOUNT = pathlib.Path(__file__).parent / "Assets" / "minesafetyar-firebase-adminsdk-fbsvc-8714f6eb7f.json"
PROJECT_ID = "minesafetyar"
LOCATION = "asia-south1"  # Mumbai - closest to Jharkhand
DATABASE_ID = "(default)"

def log(m): print(m, flush=True)

if not SERVICE_ACCOUNT.exists():
    log(f"[FAIL] Service account not found: {SERVICE_ACCOUNT}")
    sys.exit(1)

log(f"[INFO] Using service account: {SERVICE_ACCOUNT}")
log(f"[INFO] Project: {PROJECT_ID} Location: {LOCATION}")

try:
    from google.oauth2 import service_account
    import google.auth.transport.requests
    import requests
except ImportError as e:
    log(f"[FAIL] Missing deps: {e} -> pip install google-auth requests")
    sys.exit(1)

# Load credentials with cloud-platform scope (needed for Firestore Admin)
scopes = ["https://www.googleapis.com/auth/cloud-platform", "https://www.googleapis.com/auth/datastore"]
creds = service_account.Credentials.from_service_account_file(str(SERVICE_ACCOUNT), scopes=scopes)
request = google.auth.transport.requests.Request()
creds.refresh(request)
token = creds.token
log(f"[INFO] Got OAuth token (len {len(token)})")

headers = {
    "Authorization": f"Bearer {token}",
    "Content-Type": "application/json"
}

# Check if database already exists
check_url = f"https://firestore.googleapis.com/v1/projects/{PROJECT_ID}/databases/{DATABASE_ID}"
log(f"[INFO] Checking existing DB: {check_url}")
resp = requests.get(check_url, headers=headers)
if resp.status_code == 200:
    info = resp.json()
    log(f"[OK] Firestore DB already exists: {json.dumps(info, indent=2)}")
    # Also check if it's Firestore Native vs Datastore
    log(f"[OK] No creation needed. You can now retry Unity -> Test: Register")
    sys.exit(0)
else:
    log(f"[INFO] Check status {resp.status_code}: {resp.text[:500]}")

# Create database
create_url = f"https://firestore.googleapis.com/v1/projects/{PROJECT_ID}/databases"
# Alternative endpoint also works: https://firestore.googleapis.com/v1/projects/{PROJECT_ID}/databases
body = {
    "name": f"projects/{PROJECT_ID}/databases/{DATABASE_ID}",
    "locationId": LOCATION,
    "type": "FIRESTORE_NATIVE",
}

# Some API versions require ?databaseId=(default) query param
create_url_q = f"{create_url}?databaseId={DATABASE_ID}"
log(f"[INFO] Creating Firestore Native DB at {LOCATION} ...")
log(f"[INFO] POST {create_url_q}")
log(f"[INFO] Body: {json.dumps(body)}")

resp = requests.post(create_url_q, headers=headers, json=body)
log(f"[INFO] Response {resp.status_code}: {resp.text[:2000]}")

if resp.status_code in (200, 201):
    log("[OK] Firestore DB creation started (200/201)")
    op = resp.json()
    # If long-running operation, poll
    if "name" in op and "operations" in op["name"]:
        op_name = op["name"]
        log(f"[INFO] Operation {op_name}, polling...")
        for i in range(30):
            time.sleep(5)
            r2 = requests.get(f"https://firestore.googleapis.com/v1/{op_name}", headers=headers)
            log(f"[POLL {i}] {r2.status_code}: {r2.text[:1000]}")
            if r2.status_code == 200:
                j = r2.json()
                if j.get("done"):
                    if j.get("error"):
                        log(f"[FAIL] Operation failed: {j['error']}")
                        sys.exit(1)
                    log("[OK] Firestore DB ready!")
                    sys.exit(0)
        log("[WARN] Poll timeout — check console.firebase.google.com -> Firestore Database manually")
    else:
        log("[OK] DB created (sync). You can now test Unity.")
    sys.exit(0)
elif resp.status_code == 409:
    log("[OK] DB already exists (409 conflict) — ready to use")
    sys.exit(0)
elif resp.status_code == 400 and "already exists" in resp.text:
    log("[OK] DB already exists (400) — ready")
    sys.exit(0)
else:
    # Fallback: try alternative AppEngine linked method via Firebase?
    log(f"[FAIL] Creation failed {resp.status_code}. Manual fallback:")
    log(f"  Open https://console.firebase.google.com/project/{PROJECT_ID}/firestore -> Create database -> Test mode -> {LOCATION}")
    log(f"  Or https://console.cloud.google.com/datastore/setup?project={PROJECT_ID}")
    sys.exit(1)
