#!/usr/bin/env bash
# Checks whether the provider API accepts the token in an env file. Never prints the token.
set -uo pipefail

env_file="${1:-.env}"
if [[ ! -f "$env_file" ]]; then
    echo "No env file at $env_file. Create it from .env.example first." >&2
    exit 2
fi

read_value() {
    grep -E "^$1=" "$env_file" | tail -1 | cut -d= -f2- | tr -d '\r"'
}

base_url="$(read_value API_BASE_URL)"
token="$(read_value API_TOKEN)"
if [[ -z "$base_url" || -z "$token" ]]; then
    echo "$env_file must set API_BASE_URL and API_TOKEN." >&2
    exit 2
fi

accepted=0
rejected=0

for provider in cinemaworld filmworld; do
    status=000
    # The API is known to be slow and flaky, so one bad response proves nothing.
    for attempt in 1 2 3 4; do
        # The header goes over stdin so the token never appears in the process list.
        status="$(printf 'x-access-token: %s\n' "$token" | curl -sS -o /dev/null -m 30 -w '%{http_code}' -H @- \
            "$base_url/api/$provider/movies" 2>/dev/null)"
        [[ "$status" == 200 || "$status" == 401 || "$status" == 403 ]] && break
    done

    case "$status" in
        200) echo "$provider: token accepted"; accepted=$((accepted + 1)) ;;
        401 | 403) echo "$provider: token REJECTED (HTTP $status)"; rejected=$((rejected + 1)) ;;
        *) echo "$provider: no usable answer after 4 attempts (last status $status)" ;;
    esac
done

if ((rejected > 0)); then
    echo "Result: the token is not valid."
    exit 1
elif ((accepted == 0)); then
    echo "Result: inconclusive. The API did not answer, so the token wasn't tested."
    exit 3
fi
echo "Result: the token is valid."
