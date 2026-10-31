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

## Run It Locally
The only requirement is Docker.

```bash
cp .env.example .env        # then set API_BASE_URL and API_TOKEN. It's ignored by git
make check-upstream         # confirms the token works
make deploy-local           # http://localhost:8080
make logs
make down
```

## Run It Without A Token
```bash
make demo
```

This runs the production image against a fake provider API, so you can break a provider and watch
the app respond:

```bash
curl -X POST localhost:8081/__chaos/filmworld/mode/error   # or hang, flaky, garbage, empty, prices-error, ok
curl -X POST localhost:8081/__chaos/filmworld/price/0076759/9.99
curl -X POST localhost:8081/__chaos/reset
make down
```

`make smoke` runs the same stack and checks it automatically.

## Testing
```bash
make test
```

The backend tests run in Docker. To work outside Docker, use the .NET 10 SDK and Bun.
`bun run dev` serves the frontend on port 3000 and proxies `/api` to the app on port 8080.

After changing the API, run `make openapi` to update `backend/openapi/v1.json` and the generated
frontend types. A test fails if they drift.

## Deployment
`compose.yaml` runs the same way locally and on a server. The `tls` profile adds Caddy, which handles
HTTPS and renews certificates automatically. Any Linux server with Docker works.

1. Point a DNS A record at the server, and open ports 80 and 443.
2. Add `DOMAIN` and `DEPLOY_HOST` to `.env`.
3. Run `make deploy`. It uses SSH, so nothing needs copying to the server.

`make deploy` pulls the image CI publishes to `ghcr.io/cowza/movie-comparison`. If that package is
private, run `docker login ghcr.io` first.

### Continuous Deployment
Every push and pull request runs both test suites and a smoke test of the production image. Each
push to `master` then publishes the image.

Deploying from CI stays off until you add these to the repository:

| Type | Name | Value |
| --- | --- | --- |
| Variable | `DEPLOY_HOST` | `user@host`, which turns deployment on |
| Variable | `DEPLOY_KNOWN_HOSTS` | Output of `ssh-keyscan <host>` |
| Variable | `DOMAIN` | Public hostname |
| Variable | `API_BASE_URL` | Provider API URL |
| Secret | `DEPLOY_SSH_KEY` | Private key for that user |
| Secret | `API_TOKEN` | Provider API token |

### Operations
- The container is read-only, runs as non-root and has no Linux capabilities.
- Its healthcheck uses `/health/live`, so a provider outage never restarts it. `/health/ready` also
  fails when no provider has usable prices.
- The app port is bound to loopback, so public traffic has to come through Caddy. That's what makes
  it safe to trust the forwarded client address for rate limits.
- Set `OTEL_EXPORTER_OTLP_ENDPOINT` to send traces, metrics and logs to a collector.
