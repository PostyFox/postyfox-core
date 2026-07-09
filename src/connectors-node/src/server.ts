import Fastify, { type FastifyInstance } from "fastify";
import {
  createDefaultRegistry,
  resolveConnector,
  type ConnectorRegistry,
} from "./connectors/index.js";
import type { ConnectorContext, Post } from "./types.js";

export interface BuildServerOptions {
  /** Connector registry. Defaults to the real Bluesky + Tumblr connectors. */
  registry?: ConnectorRegistry;
  /**
   * Expected internal token. If undefined, auth is disabled (dev mode).
   * Pass `process.env.INTERNAL_TOKEN` from the entrypoint.
   */
  internalToken?: string;
  /** Fastify logger toggle. */
  logger?: boolean;
}

interface DeliverBody {
  context: ConnectorContext;
  post: Post;
}

export function buildServer(options: BuildServerOptions = {}): FastifyInstance {
  const registry = options.registry ?? createDefaultRegistry();
  const internalToken = options.internalToken;

  const app = Fastify({ logger: options.logger ?? false });

  // Auth middleware: everything except /health requires a valid token.
  app.addHook("onRequest", async (request, reply) => {
    if (request.url === "/health") return;
    // If no token configured, allow all (dev).
    if (internalToken === undefined || internalToken === "") return;

    const provided = request.headers["x-internal-token"];
    if (provided !== internalToken) {
      reply.code(401).send({ error: "unauthorized" });
    }
  });

  app.get("/health", async () => ({ status: "ok" }));

  app.post<{ Params: { platform: string }; Body: ConnectorContext }>(
    "/connectors/:platform/is-authenticated",
    async (request, reply) => {
      const connector = resolveConnector(registry, request.params.platform);
      if (!connector) return reply.code(404).send({ error: "unknown platform" });
      return connector.isAuthenticated(request.body);
    },
  );

  app.post<{ Params: { platform: string }; Body: ConnectorContext }>(
    "/connectors/:platform/list-targets",
    async (request, reply) => {
      const connector = resolveConnector(registry, request.params.platform);
      if (!connector) return reply.code(404).send({ error: "unknown platform" });
      return connector.listTargets(request.body);
    },
  );

  app.post<{ Params: { platform: string }; Body: DeliverBody }>(
    "/connectors/:platform/deliver",
    async (request, reply) => {
      const connector = resolveConnector(registry, request.params.platform);
      if (!connector) return reply.code(404).send({ error: "unknown platform" });
      const { context, post } = request.body;
      return connector.deliver(context, post);
    },
  );

  return app;
}
