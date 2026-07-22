# ---- Build stage ----
FROM node:24 AS build
WORKDIR /app

COPY package.json package-lock.json* ./
RUN npm ci

COPY tsconfig.json ./
COPY src ./src
RUN npm run build

# Prune dev dependencies for the runtime image.
RUN npm prune --omit=dev

# ---- Runtime stage ----
FROM node:24-slim AS runtime
WORKDIR /app
ENV NODE_ENV=production
ENV PORT=8090

# Object store (S3-compatible) configuration for media fetch. Provide the
# secrets/endpoint at runtime (`docker run -e ...`); defaults shown here.
#   OBJECT_STORE_SERVICE_URL   - S3 endpoint, e.g. http://minio:9000
#   OBJECT_STORE_ACCESS_KEY    - access key id
#   OBJECT_STORE_SECRET_KEY    - secret access key
ENV OBJECT_STORE_BUCKET=postyfox
ENV OBJECT_STORE_REGION=us-east-1
ENV OBJECT_STORE_FORCE_PATH_STYLE=true

COPY package.json ./
COPY instrumentation.mjs ./
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/dist ./dist

EXPOSE 8090
# --import runs our OpenTelemetry bootstrap (instrumentation.mjs) before app code: auto-instrumentation
# for traces/metrics/logs, exporter/endpoint/service-name from OTEL_* env, and an ignore hook so the
# GET /health container check produces no span/metric.
CMD ["node", "--import", "./instrumentation.mjs", "dist/index.js"]
