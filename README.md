# Movie Price Comparison

## Overview
A web application that compares movie prices across different providers.

A background worker refreshes each provider and keeps its last good data, and requests are answered
from that snapshot. A slow or failing provider never delays a request, and every result says how
current it is. [docs/DECISIONS.md](docs/DECISIONS.md) explains why, and lists the failure modes the
tests hold it to.

## Requirements
- Handle provider service interruptions gracefully
- Secure API token management without public exposure

## Assumptions
- Movie prices update daily
- Users select movies from a dropdown interface
- Movie information displayed in card format showing available providers
- All providers prefix use a 2 letter movie prefix then use the same movie id.
- Providers may not sell the same movies
- Provider APIs may be unstable

## Run It Without A Token
The following instructions assume WSL with Docker installed (tested with Docker Desktop Windows)

```bash
make demo
```

This runs the production image at http://localhost:8080 against a fake provider API, so you can break
a provider and watch the app respond:

```bash
curl -X POST localhost:8081/__chaos/filmworld/mode/error   # or hang, flaky, garbage, empty, prices-error, ok
curl -X POST localhost:8081/__chaos/filmworld/price/0076759/9.99
curl -X POST localhost:8081/__chaos/reset
make demo-down
```

## Development Setup
1. Configure Environment
   ```bash
   mkdir -p docker/secrets
   cp .env.example docker/secrets/local-env
   ```
   Then set `ExternalApiSettings__BaseUrl` and `ExternalApiSettings__ApiToken` in that file. It's
   ignored by git.

2. Check the token works
   ```bash
   make check-upstream
   ```

3. Start the hot-reloading stack at http://localhost:3000
   ```bash
   make deploy-local
   ```

## Testing
```bash
make test
```

The backend tests run in Docker. Local work outside Docker needs the .NET 10 SDK and Bun.

After changing the API, run `make openapi` to update `backend/openapi/v1.json` and the generated
frontend types. A test fails if they drift.

## Production
CI tests every change, and publishes a single image to
`ghcr.io/cowza/movie-comparison` from `master`. It serves both the API and the frontend on port 8080,
so any container host will run it.

| Setting | Value |
| --- | --- |
| `ExternalApiSettings__BaseUrl` | Provider API URL |
| `ExternalApiSettings__ApiToken` | Provider API token, from the host's secret store |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` behind the host's proxy, so rate limits apply per client. Leave it off if the container is directly exposed |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Optional. Sends traces, metrics and logs to a collector |

Point the host's probes at `/health/live` and `/health/ready`. Readiness stays healthy while serving
stale data during an outage, and only fails when there's no data at all.
