// Custom OpenTelemetry bootstrap — mirrors @opentelemetry/auto-instrumentations-node/register
// (NodeSDK + all auto-instrumentations; exporters/resource-detectors come from the OTEL_* env),
// but adds an ignore hook so the container's GET /health check produces NO span or metric.
// Launched via `node --import ./instrumentation.mjs dist/index.js` (see Dockerfile).
import otel from "@opentelemetry/sdk-node";
import autoInstrumentations from "@opentelemetry/auto-instrumentations-node";

const { NodeSDK } = otel;
const { getNodeAutoInstrumentations } = autoInstrumentations;

const sdk = new NodeSDK({
  instrumentations: getNodeAutoInstrumentations({
    "@opentelemetry/instrumentation-http": {
      // The healthcheck hits GET /health every few seconds — drop it at the source so it never
      // becomes a span or an http.server.duration data point (node's metric has no route attribute,
      // so it can't be filtered downstream at the collector like the .NET /healthz metric is).
      ignoreIncomingRequestHook: (req) => (req.url || "").split("?")[0] === "/health",
    },
  }),
});

sdk.start();

const shutdown = async () => {
  try {
    await sdk.shutdown();
  } catch {
    /* noop */
  }
};
process.on("SIGTERM", shutdown);
process.once("beforeExit", shutdown);
