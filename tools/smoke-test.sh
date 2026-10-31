#!/usr/bin/env bash
# Checks the running demo stack, including that a broken provider doesn't reach users.
set -euo pipefail

app=http://127.0.0.1:${APP_PORT:-8080}
upstream=http://127.0.0.1:${FAKE_UPSTREAM_PORT:-8081}

fail() {
    echo "FAILED: $1" >&2
    exit 1
}

for _ in $(seq 1 60); do
    curl -fsS -m 2 "$app/api/movies" 2>/dev/null | grep -q '"complete":true' && break
    sleep 1
done
curl -fsS -m 2 "$app/api/movies" | grep -q '"complete":true' || fail "catalogue never completed"
echo "ok: catalogue loaded"

curl -fsS -m 2 "$app/" | grep -q 'id="root"' || fail "frontend not served"
echo "ok: frontend served"

curl -fsS -m 2 "$app/api/movies/0076759/offers" | grep -q '"bestOffer":{"provider":"filmworld"' || fail "wrong best offer"
echo "ok: best offer correct"

curl -fsS -m 2 -X POST "$upstream/__chaos/all/mode/hang"
seconds="$(curl -fsS -m 2 -o /dev/null -w '%{time_total}' "$app/api/movies/0076759/offers")" \
    || fail "request failed while providers were hanging"
curl -fsS -m 2 -X POST "$upstream/__chaos/reset"
echo "ok: answered in ${seconds}s while both providers were hanging"
