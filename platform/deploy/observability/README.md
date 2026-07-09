# Observability

All four services emit **OpenTelemetry** traces + metrics over **OTLP** to the endpoint in
`OTEL_EXPORTER_OTLP_ENDPOINT`. In the compose stack an `otel-collector` receives OTLP; in real
environments the collector forwards telemetry to the **central OpenSearch** cluster.

## Pipeline

```
services ──OTLP──▶ OpenTelemetry Collector ──▶ OpenSearch (central)
                                              └▶ OpenSearch Dashboards
```

Two common ways to land OTLP data in OpenSearch:

1. **Collector OpenSearch exporter** — add the `opensearch` exporter to the collector and point it
   at the central cluster.
2. **Data Prepper** — run OpenSearch Data Prepper with `otel_trace_source` / `otel_metrics_source`
   / `otel_logs_source` and an `opensearch` sink.

### Example collector exporter (option 1)

```yaml
exporters:
  opensearch:
    http:
      endpoint: "https://opensearch.internal:9200"
    # auth/tls per your cluster
service:
  pipelines:
    traces:  { receivers: [otlp], processors: [batch], exporters: [opensearch] }
    logs:    { receivers: [otlp], processors: [batch], exporters: [opensearch] }
    metrics: { receivers: [otlp], processors: [batch], exporters: [opensearch] }
```

Swap the compose collector's debug exporter (`deploy/otel/config.yaml`) for the above when wiring
the real cluster. Dashboards/queries live centrally in OpenSearch Dashboards, so none are shipped
here.

## Health / readiness

`GET /healthz` (liveness) and `GET /readyz` (DB connectivity) on both APIs; `GET /health` on
connectors-node.
