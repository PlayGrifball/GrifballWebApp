#!/usr/bin/env bash
# sablier-wake.sh: wakes an environment that Sablier scales to zero when idle, then waits for a TCP port of it.
#
# historical-archive-run.yml runs it before its copy of the test environment's database: asleep, the environment has no
# SQL Server to copy from. Sablier's blocking strategy (GET <SABLIER_URL>?group=...&session_duration=...&timeout=...)
# starts the group and answers once it's ready (200, X-Sablier-Session-Status: ready), or with an error status (404 no
# such group, 504 not ready in time). Ready is the pods' readiness; the port is then checked from here, the way the
# job will reach it.
#
# Env: SABLIER_URL (the blocking strategy's endpoint), SABLIER_GROUP, WAKE_TCP (host,port or host:port). Optional:
# SABLIER_SESSION_DURATION (30m: how long it stays up after this), SABLIER_TIMEOUT_SECONDS (180),
# WAKE_TCP_SECONDS (180), WAKE_POLL_SECONDS (3).
set -euo pipefail

: "${SABLIER_URL:?SABLIER_URL (the blocking strategy endpoint) is not set}"
: "${SABLIER_GROUP:?SABLIER_GROUP is not set}"
: "${WAKE_TCP:?WAKE_TCP (host,port) is not set}"
session="${SABLIER_SESSION_DURATION:-30m}"
timeout_seconds="${SABLIER_TIMEOUT_SECONDS:-180}"
tcp_seconds="${WAKE_TCP_SECONDS:-180}"
poll_seconds="${WAKE_POLL_SECONDS:-3}"

fail() {
  echo "::error title=Wake $SABLIER_GROUP::$1"
  exit 1
}

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# Sablier's own timeout, then a margin for the request itself
echo "Waking $SABLIER_GROUP through Sablier (up to ${timeout_seconds}s)"
started=$SECONDS
code=$(curl --silent --show-error --get --max-time $((timeout_seconds + 30)) \
  --data-urlencode "group=$SABLIER_GROUP" --data-urlencode "session_duration=$session" \
  --data-urlencode "timeout=${timeout_seconds}s" \
  --output "$work/body" --dump-header "$work/headers" --write-out '%{http_code}' "$SABLIER_URL" 2> "$work/error") || true
if [[ -z "$code" || "$code" = 000 ]]; then
  fail "Sablier didn't answer at $SABLIER_URL: $(tr '\n' ' ' < "$work/error")"
fi
if [[ "$code" != 2?? ]]; then
  fail "Sablier answered $code for group $SABLIER_GROUP: $(head -c 500 "$work/body" | tr '\n' ' ')"
fi
status=$(sed -n 's/^[Xx]-[Ss]ablier-[Ss]ession-[Ss]tatus: *//p' "$work/headers" | tr -d '\r' | tail -n 1)
if [[ -n "$status" && "$status" != ready ]]; then
  fail "Sablier answered $code but the group $SABLIER_GROUP is $status"
fi
echo "$SABLIER_GROUP is ready ($((SECONDS - started))s)"

# host,port (SQL Server's spelling) or host:port
host=${WAKE_TCP%[,:]*}
port=${WAKE_TCP##*[,:]}
[[ "$host" != "$WAKE_TCP" && "$port" =~ ^[0-9]+$ ]] || fail "WAKE_TCP is not host,port: $WAKE_TCP"
started=$SECONDS
until timeout 5 bash -c 'exec 3<>"/dev/tcp/$1/$2"' _ "$host" "$port" 2> /dev/null; do
  if (( SECONDS - started >= tcp_seconds )); then
    fail "$host:$port didn't accept a connection in ${tcp_seconds}s after $SABLIER_GROUP was ready"
  fi
  sleep "$poll_seconds"
done
echo "$host:$port accepts connections ($((SECONDS - started))s)"
