# Decisions

## Requests never call providers

The provider APIs are slow and unreliable, and their prices change about daily. A worker
(`SnapshotRefreshWorker`) refreshes each provider on its own loop and keeps the last good data.
Requests only read that snapshot.

The cost is that a price can be as old as the last successful refresh. The API states this rather
than hiding it: every offer carries `asOf`, and older data is flagged or dropped.

## How old data is handled

| Age of the data | Behaviour | Setting |
| --- | --- | --- |
| Up to 15 minutes | Shown normally | `Snapshot:StaleAfter` |
| 15 minutes to 24 hours | Shown with `stale: true`, and the result is not `complete` | `Snapshot:MaxDataAge` |
| Over 24 hours | Dropped, because prices change daily | |

A stale price can still be the best offer. It's the most useful answer available, and the UI labels
it as last known.

## `complete` decides whether we can say "best price"

A result is complete only when every provider either has a current price for the movie or has a
current catalogue that doesn't include it. Otherwise the provider appears in `unavailableProviders`
and the UI says "lowest known price". A movie that only one provider sells can still be complete.

## Snapshots replace the cache

There is no `IMemoryCache` or `HybridCache`. With only the worker calling providers, requests have
nothing to cache and no stampede to protect against. Serving last good data was also the one thing
those caches wouldn't do.

Snapshots live in memory, so a restart during an outage loses them until a provider next answers.
Running more than one instance would need a shared store behind `ISnapshotStore`.

## Providers are configuration

Every provider shares one API shape, so `HttpMovieProvider` covers them all. Adding one means adding
an entry to `ExternalApiSettings:Providers`, if it matches that shape. Each provider gets its own named
HTTP client, because sharing a name stacks their resilience handlers and shares a circuit breaker.

## Failure modes

These are the acceptance spec. Each row has a test in
`backend/tests/MovieComparison.IntegrationTests/FailureModeTests.cs`, which runs the real app against
`MovieComparison.FakeUpstream` over real HTTP.

| Failure | Required behaviour |
| --- | --- |
| One provider returns 500s | At most 4 attempts per call. The other provider is unaffected |
| Movie sold by only one provider | Complete result, and no call to the other provider for it |
| Provider down from cold start | Reported in `unavailableProviders`, never hidden |
| Cheaper provider goes down | Its last price stays, flagged stale |
| Provider hangs | Requests unaffected. Timeouts are retried |
| Provider flaky | Retries succeed within one refresh |
| Provider keeps failing | Circuit breaker stops calling it |
| Both down, nothing loaded | 503 with `Retry-After`. Not ready, but live |
| Both down, data loaded | Last good data served. Ready, but degraded |
| Provider recovers | Next refresh restores the full result |
| 20 concurrent lookups | No upstream calls |
| Unknown or malicious ID | 404 with no upstream call |
| Upstream 4xx | Not retried |
| Invalid price | Last good price kept, never shown as null |
| Empty or garbage catalogue | Snapshot kept |
| Token rejected | Not retried, and never exposed |
