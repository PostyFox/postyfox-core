#!/bin/sh
# PostyFox Vault bootstrap + auto-unseal + app-auth provisioning sidecar.
#
# Runs alongside the `vault` service and:
#   1. Waits for Vault to answer.
#   2. Initialises it on first boot, saving the generated unseal keys + root token to the mounted
#      keys volume ($KEY_FILE). These are the Shamir seal keys — treat the volume as a secret.
#   3. Watches Vault forever and re-applies the saved keys whenever it is found sealed (first boot,
#      after a restart, or after a crash), so the stack comes up unsealed with no manual step.
#   4. Provisions the app's secret store once Vault is unsealed: a KV v2 mount, a scoped policy, and
#      an AppRole whose RoleId/SecretId are PINNED to the values supplied via env ($VAULT_ROLE_ID /
#      $VAULT_SECRET_ID). Pinning is what lets the API/worker containers authenticate with AppRole
#      credentials they already hold from `.env` — no runtime token hand-off needed.
#
# NOTE: storing the unseal keys next to the server is what makes unattended unsealing possible; it
# trades the Shamir key-splitting guarantee for convenience. For a stronger posture switch to a
# Transit / cloud-KMS auto-unseal seal and drop this sidecar.
set -u

VAULT_ADDR="${VAULT_ADDR:-http://vault:8200}"
export VAULT_ADDR

KEY_FILE="/vault/init/init.json"
KEY_SHARES="${VAULT_KEY_SHARES:-5}"
KEY_THRESHOLD="${VAULT_KEY_THRESHOLD:-3}"

# App secret-store provisioning (KV v2 + AppRole). Provisioning is skipped unless BOTH the RoleId and
# SecretId are supplied — that keeps this a no-op when the stack is pointed at a different provider.
APP_MOUNT="${VAULT_MOUNT:-secret}"
APP_BASE_PATH="${VAULT_BASE_PATH:-postyfox}"
APP_ROLE_NAME="${VAULT_APP_ROLE_NAME:-postyfox}"
APP_POLICY_NAME="${VAULT_APP_POLICY_NAME:-postyfox}"
APP_ROLE_ID="${VAULT_ROLE_ID:-}"
APP_SECRET_ID="${VAULT_SECRET_ID:-}"

log() { echo "[vault-bootstrap] $*"; }

# Field lookup from `vault status` (works regardless of seal state / output format churn).
status_field() { vault status 2>/dev/null | awk -v f="$1" '$1 == f {print $2}'; }

# --- 1. Wait for Vault to be reachable (sealed==exit 2 and unsealed==exit 0 both count) ----------
log "waiting for Vault at ${VAULT_ADDR} ..."
until vault status >/dev/null 2>&1 || [ $? -eq 2 ]; do
  sleep 2
done

# --- 2. Initialise on first boot -----------------------------------------------------------------
if [ "$(status_field Initialized)" = "true" ]; then
  log "Vault already initialised"
else
  log "initialising Vault (key-shares=${KEY_SHARES} key-threshold=${KEY_THRESHOLD})"
  if ! vault operator init \
      -key-shares="$KEY_SHARES" \
      -key-threshold="$KEY_THRESHOLD" \
      -format=json > "$KEY_FILE"; then
    log "ERROR: vault operator init failed"
    rm -f "$KEY_FILE"
    exit 1
  fi
  chmod 600 "$KEY_FILE" 2>/dev/null || true
  log "unseal keys + root token written to ${KEY_FILE}"
fi

if [ ! -f "$KEY_FILE" ]; then
  log "ERROR: Vault is initialised but ${KEY_FILE} is missing — cannot auto-unseal."
  log "       (Did the keys volume get wiped while the data volume survived?)"
  exit 1
fi

# Flatten the pretty-printed init JSON once (no jq in the Vault image) for the extracts below.
FLAT=$(tr -d '\n\r\t ' < "$KEY_FILE")

# Extract the base64 unseal keys: isolate the "unseal_keys_b64" array, one key per line.
UNSEAL_KEYS=$(printf '%s' "$FLAT" | sed 's/.*"unseal_keys_b64":\[//; s/\].*//' | tr ',' '\n' | tr -d '"')
# Extract the initial root token (used to provision the app mount / policy / AppRole below).
ROOT_TOKEN=$(printf '%s' "$FLAT" | sed 's/.*"root_token":"//; s/".*//')

if [ -z "$UNSEAL_KEYS" ]; then
  log "ERROR: no unseal keys found in ${KEY_FILE}"
  exit 1
fi

apply_unseal() {
  echo "$UNSEAL_KEYS" | while IFS= read -r key; do
    [ -n "$key" ] || continue
    # Stop feeding keys once the threshold is met and Vault reports unsealed.
    if [ "$(status_field Sealed)" = "false" ]; then
      break
    fi
    vault operator unseal "$key" >/dev/null 2>&1 || true
  done
}

# --- App secret-store provisioning (idempotent) --------------------------------------------------
# Enable a KV v2 mount, install a scoped policy, and create an AppRole with a PINNED RoleId/SecretId
# so the app can authenticate with the credentials it already has from `.env`. Every step is safe to
# re-run: mount/auth enable are treated as no-ops if already present, and policy/role writes are
# declarative overwrites.
provision_app_auth() {
  if [ -z "$APP_ROLE_ID" ] || [ -z "$APP_SECRET_ID" ]; then
    log "app AppRole provisioning skipped (VAULT_ROLE_ID / VAULT_SECRET_ID not set)"
    return 0
  fi
  if [ -z "$ROOT_TOKEN" ]; then
    log "WARN: no root token in ${KEY_FILE} — cannot provision app AppRole"
    return 0
  fi

  VAULT_TOKEN="$ROOT_TOKEN"
  export VAULT_TOKEN

  # KV v2 mount for the app's secrets (the file backend does not auto-mount one).
  if vault secrets list -format=json 2>/dev/null | grep -q "\"${APP_MOUNT}/\""; then
    : # mount already present
  else
    log "enabling KV v2 secrets engine at '${APP_MOUNT}/'"
    vault secrets enable -path="$APP_MOUNT" kv-v2 >/dev/null 2>&1 || true
  fi

  # Scoped policy — exactly the KV v2 paths the adapter touches under the base path.
  log "writing policy '${APP_POLICY_NAME}'"
  vault policy write "$APP_POLICY_NAME" - >/dev/null 2>&1 <<EOF
path "${APP_MOUNT}/data/${APP_BASE_PATH}/*" {
  capabilities = ["create", "read", "update", "delete"]
}
path "${APP_MOUNT}/metadata/${APP_BASE_PATH}/*" {
  capabilities = ["read", "list", "delete"]
}
path "${APP_MOUNT}/metadata/${APP_BASE_PATH}" {
  capabilities = ["read", "list"]
}
EOF

  # AppRole auth backend.
  if vault auth list -format=json 2>/dev/null | grep -q '"approle/"'; then
    : # already enabled
  else
    log "enabling AppRole auth backend"
    vault auth enable approle >/dev/null 2>&1 || true
  fi

  # The role: the app's token carries only the scoped policy. Non-expiring SecretId (num_uses=0,
  # ttl=0) so long-running containers keep authenticating without a rotation hand-off.
  log "configuring AppRole role '${APP_ROLE_NAME}'"
  vault write "auth/approle/role/${APP_ROLE_NAME}" \
    token_policies="$APP_POLICY_NAME" \
    secret_id_num_uses=0 \
    secret_id_ttl=0 \
    token_ttl=1h \
    token_max_ttl=4h >/dev/null 2>&1 || true

  # Pin the RoleId + register the SecretId to the operator-supplied values so they match `.env`.
  vault write "auth/approle/role/${APP_ROLE_NAME}/role-id" \
    role_id="$APP_ROLE_ID" >/dev/null 2>&1 || true
  vault write "auth/approle/role/${APP_ROLE_NAME}/custom-secret-id" \
    secret_id="$APP_SECRET_ID" >/dev/null 2>&1 || true

  log "app AppRole '${APP_ROLE_NAME}' ready (mount='${APP_MOUNT}' base='${APP_BASE_PATH}')"

  unset VAULT_TOKEN
}

# --- 3. Ensure unsealed, then provision the app auth ---------------------------------------------
if [ "$(status_field Sealed)" != "false" ]; then
  log "Vault is sealed — unsealing"
  apply_unseal
fi
if [ "$(status_field Sealed)" = "false" ]; then
  provision_app_auth
fi

# --- 4. Watch + auto-unseal forever --------------------------------------------------------------
log "entering unseal watch loop"
while true; do
  case "$(status_field Sealed)" in
    true)
      log "Vault is sealed — unsealing"
      apply_unseal
      if [ "$(status_field Sealed)" = "false" ]; then
        log "Vault unsealed"
        # Re-assert the app auth after an unseal in case this is a fresh data volume.
        provision_app_auth
      else
        log "WARN: Vault still sealed after applying keys"
      fi
      ;;
    false)
      : # unsealed, nothing to do
      ;;
    *)
      : # unreachable (e.g. mid-restart); retry next tick
      ;;
  esac
  sleep 5
done
