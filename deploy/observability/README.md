# Observability

All four services emit **OpenTelemetry** traces + metrics over **OTLP** to the endpoint in
`OTEL_EXPORTER_OTLP_ENDPOINT`. In the compose stack an `otel-collector` receives OTLP; in real
environments the collector forwards telemetry a the **central OpenSearch** cluster, or if you are
deploying into Azure, you can pipe the information to **Azure Monitor** / **Application Insights**.

## Pipeline

```
services ──OTLP──▶ OpenTelemetry Collector ──OTLP──▶ Data Prepper ──▶ OpenSearch (central)
```

The compose stack now includes both:

- `deploy/otel/config.yaml` (collector receives OTLP from services and forwards to Data Prepper)
- `deploy/data-prepper/pipelines.yaml` (Data Prepper OTEL trace/metrics/log sources)

Two common ways to land OTLP data in OpenSearch:

1. **Collector OpenSearch exporter** — add the `opensearch` exporter to the collector and point it
   at the central cluster.
2. **Data Prepper** — run OpenSearch Data Prepper with `otlp_traces` / `otlp_metrics`
   / `otlp_logs` and an `opensearch` sink.

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

Swap the compose defaults (`deploy/otel/config.yaml` and `deploy/data-prepper/pipelines.yaml`) for
your central OpenSearch settings when wiring the real cluster. Dashboards/queries live centrally in
OpenSearch Dashboards, so none are shipped here.

## Health / readiness

`GET /healthz` (liveness) and `GET /readyz` (DB connectivity) on both APIs; `GET /health` on
connectors-node.

## Azure specifics

If you are wanting to pipe the information into, for example, Application Insights, you can use the 
`azuremonitor` exporter in the collector. You will need to set the `OTEL_EXPORTER_AZUREMONITOR_CONNECTION_STRING` 
environment variable with your Application Insights connection string.
