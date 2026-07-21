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
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/dist ./dist

EXPOSE 8090
# --import registers OpenTelemetry auto-instrumentation (traces/metrics/logs) before app code.
# Exporter/endpoint/service-name come from OTEL_* env (set in docker-compose). No-op if unset.
CMD ["node", "--import", "@opentelemetry/auto-instrumentations-node/register", "dist/index.js"]
