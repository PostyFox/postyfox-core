# Observability

All services emit **OpenTelemetry** traces, metrics, and logs over **OTLP** to the endpoint in
`OTEL_EXPORTER_OTLP_ENDPOINT` (`OTEL_ENDPOINT` in the env files) — always the *local* edge
collector. What happens after the edge collector depends on the stack:

- **Bare local stack** (`docker-compose.yml`): the collector prints telemetry to its own logs, so
  the stack is observable with no external dependency — `docker compose logs otel-collector`.
- **Deployed stacks** (`docker-compose.server.yml` + dev/prod overrides): the collector shapes and
  forwards OTLP to a **central OpenSearch Data Prepper**, which writes to the shared OpenSearch
  cluster. Or, on Azure, point the collector at **Azure Monitor** / **Application Insights** (see
  below).

## Topology

```
                          reduce traffic here                 heavy lifting here
                          (batch · gzip · filter · sample)     (OTel schema · service maps · index
                                                                templates · OpenSearch auth + certs)
each env:  app SDKs ─OTLP─▶ local OTel Collector ──OTLP──▶ ┐
                                                           ├─▶ CENTRAL Data Prepper ──▶ OpenSearch
other env: app SDKs ─OTLP─▶ local OTel Collector ──OTLP──▶ ┘        │                    (central)
                                                                    └─────────────▶ OpenSearch Dashboards
```

**Division of labour:** the edge collector is the *only* place to reduce volume before it crosses
the wire; the central Data Prepper is the *only* place that holds OpenSearch credentials + certs and
owns the OpenSearch Observability-plugin schema, service maps, and index templates. Per-environment
app stacks therefore hold **no OpenSearch secrets**.

### Edge collector (this repo)

- `deploy/otel/config.yaml` — bare-local: OTLP in → `debug` (stdout) out.
- `deploy/otel/config.central.yaml` — deployed: OTLP in → shape → OTLP out to `OTEL_CENTRAL_HOST`
  (traces `21890`, metrics `21891`, logs `21892`). Set `OTEL_CENTRAL_HOST` in the env file.

Traffic-reduction levers, all in `config.central.yaml`:

- **gzip compression** on every exporter (on by default).
- **batching** — fewer, larger requests (`batch` processor).
- **health-check filtering** — `/healthz`, `/readyz`, `/health` spans are dropped (on by default).
- **tail sampling** (opt-in, commented) — keep all errors + slow traces, sample the rest. Must live
  in the collector because it needs whole traces before fan-out. With sampling on, central service
  maps / RED metrics become statistical; add the collector `spanmetrics` connector if you need exact
  metrics alongside sampled traces.

## Central Data Prepper (deployed with the OpenSearch cluster, not in this repo)

Run one shared Data Prepper next to the central cluster. Example `pipelines.yaml` landing data in the
Observability-plugin indices:

```yaml
entry-pipeline-traces:
  source:
    otlp_traces: { ssl: false, port: 21890 }   # front with TLS at the network edge if exposed
  processor:
    - otel_traces:            # normalise spans into the OTel-standard raw-trace schema
  sink:
    - opensearch:
        hosts: ["https://opensearch.internal:9200"]
        username: "${OPENSEARCH_USER}"          # internal user with write perms, created cluster-side
        password: "${OPENSEARCH_PASSWORD}"
        cert: "/usr/share/data-prepper/certs/opensearch-ca.pem"   # the cluster's CA (public cert)
        # insecure: true                        # dev-only: skip TLS verification instead of a CA
        index_type: trace-analytics-raw         # otel-v1-apm-span-* — powers Trace Analytics
service-map-pipeline:
  source:
    pipeline: { name: entry-pipeline-traces }
  processor:
    - service_map:            # builds the service map from the same span stream
  sink:
    - opensearch:
        hosts: ["https://opensearch.internal:9200"]
        username: "${OPENSEARCH_USER}"
        password: "${OPENSEARCH_PASSWORD}"
        cert: "/usr/share/data-prepper/certs/opensearch-ca.pem"
        index_type: trace-analytics-service-map

entry-pipeline-metrics:
  source: { otlp_metrics: { ssl: false, port: 21891 } }
  sink:
    - opensearch:
        hosts: ["https://opensearch.internal:9200"]
        username: "${OPENSEARCH_USER}"
        password: "${OPENSEARCH_PASSWORD}"
        cert: "/usr/share/data-prepper/certs/opensearch-ca.pem"
        index: otel-metrics-%{yyyy.MM.dd}

entry-pipeline-logs:
  source: { otlp_logs: { ssl: false, port: 21892 } }
  sink:
    - opensearch:
        hosts: ["https://opensearch.internal:9200"]
        username: "${OPENSEARCH_USER}"
        password: "${OPENSEARCH_PASSWORD}"
        cert: "/usr/share/data-prepper/certs/opensearch-ca.pem"
        index: otel-logs-%{yyyy.MM.dd}
```

### Auth from Data Prepper to OpenSearch — what you need

- **No client certificates.** Data Prepper authenticates with a **username/password** (self-managed,
  OpenSearch Security plugin internal user) or an **`aws:` block** (AWS OpenSearch / IAM SigV4). Both
  are created cluster-side, not here.
- **TLS is separate from auth.** OpenSearch serves HTTPS on 9200; point the sink `cert:` at the
  **CA that signed the OpenSearch node cert** (a public cert you copy from the cluster) so Data
  Prepper can verify the server. Use `insecure: true` only for dev. A client cert is needed *only* if
  the cluster is configured for mutual TLS — uncommon, and not required here.

Dashboards/queries live centrally in OpenSearch Dashboards, so none are shipped here.

## Health / readiness

`GET /healthz` (liveness) and `GET /readyz` (DB connectivity) on both APIs; `GET /health` on
connectors-node. These routes are filtered out of traces at the edge collector.

## Azure specifics

To pipe telemetry into Application Insights instead, use the `azuremonitor` exporter in the collector
and set `OTEL_EXPORTER_AZUREMONITOR_CONNECTION_STRING` to your Application Insights connection string.
