---
id: operations
title: Operations
sidebar_label: Operations
---

The production host exposes readiness, Prometheus metrics, scheduled OCI garbage collection, retention, and audit facilities. Keep operational endpoints on a private network or restrict them at the reverse proxy.

## Prometheus metrics

Prometheus metrics are available at `GET /metrics`. Feed metrics use only the bounded `feed` and `type` labels so package names, repository names, versions, and digests do not create unbounded time series.

| Metric | Type | Meaning |
|--------|------|---------|
| `feed_push_total{feed,type}` | Counter | Completed top-level artifact uploads |
| `feed_pull_total{feed,type}` | Counter | Completed top-level artifact downloads |
| `blob_bytes_stored{feed,type}` | Gauge | Bytes currently held in OCI content-addressed storage |

Example Prometheus configuration:

```yaml
scrape_configs:
  - job_name: avantipoint-packages
    metrics_path: /metrics
    static_configs:
      - targets: [packages.internal.example:8080]
```

The metrics endpoint is anonymous so a scraper can reach it without a feed API token. Do not expose it directly to the public internet. Apply an IP allowlist, private ingress, or an authentication policy at the reverse proxy.

## Readiness and liveness

The production host exposes these endpoints:

| Endpoint | Purpose |
|----------|---------|
| `GET /health` | Host readiness, including the package catalog and host identity databases |
| `GET /health/feeds` | Readiness and registered surfaces for the configured feed |
| `GET /health/feeds/{feedId}` | The same report for a specific feed ID; an unknown ID returns `404` |

A feed readiness response is `200` when all registered health checks are healthy and `503` otherwise. The response includes each registered NuGet, npm, and OCI surface plus the underlying health-check results. Use `/health` or `/health/feeds/{feedId}` for readiness probes.

## OCI garbage collection

OCI garbage collection uses mark and sweep. It starts from every repository tag, recursively follows image indexes and manifests, and deletes only blobs that are not reachable from that graph. A minimum age protects uploads that are still in progress. Dry-run mode is the default.

Configure the scheduler under `Feed:Operations:OciGarbageCollection`:

```json
{
  "Feed": {
    "Operations": {
      "OciGarbageCollection": {
        "Enabled": true,
        "DryRun": true,
        "Interval": "1.00:00:00",
        "MinimumAge": "1.00:00:00"
      }
    }
  }
}
```

Recommended rollout:

1. Enable the scheduler with `DryRun` set to `true`.
2. Review the structured GC logs for at least one full retention interval. Each run reports the feed, OCI segment, candidate count, candidate bytes, and dry-run state.
3. Confirm that `MinimumAge` exceeds the longest expected image push duration.
4. Set `DryRun` to `false` after the candidate set is understood.
5. Monitor `blob_bytes_stored{type="oci"}` and storage-provider capacity alerts.

GC processes each OCI surface independently. A failure reading a reachable manifest aborts that surface's run before any candidate is selected, preventing collection from continuing with an incomplete reference graph. Deletions are persisted one blob at a time so a later storage failure does not leave earlier deletions recorded only in memory.

NuGet prerelease retention is configured separately under `Retention`; see the retention settings in the [configuration guide](configuration.md). Storage quotas are not enforced by an application quota UI. Use storage-provider quotas and alerts together with `blob_bytes_stored`.

## API key rotation

Rotate feed keys without downtime:

1. Create a replacement token under **API Keys** with the same required scopes.
2. Update clients, CI secrets, and registry credentials to use the replacement.
3. Verify successful package restore and publish operations in the audit log.
4. Revoke the old token under **API Keys**.
5. Remove the old value from every external secret store.

Use short-lived tokens where practical. Never place tokens in image layers, source control, command history, or Prometheus labels.

## Production transport and audit

Terminate TLS at Kestrel or a trusted reverse proxy and forward the original scheme and host. Production public URLs must be HTTPS. Restrict administration pages, health details, and metrics to trusted networks.

The host records package publication and group promotion events in its audit log and can dispatch configured webhooks. Review the audit log after key rotation, GC configuration changes, and failed downstream publishing attempts.
