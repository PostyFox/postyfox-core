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

  // logLevel: silent — the container healthcheck hits this constantly; don't emit access logs for it.
  app.get("/health", { logLevel: "silent" }, async () => ({ status: "ok" }));

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

  app.post<{ Params: { platform: string }; Body: ConnectorContext }>(
    "/connectors/:platform/limits",
    async (request, reply) => {
      const connector = resolveConnector(registry, request.params.platform);
      if (!connector) return reply.code(404).send({ error: "unknown platform" });
      if (!connector.getLimits) return reply.code(400).send({ error: "limits not supported" });
      return connector.getLimits(request.body);
    },
  );

  app.post<{ Params: { platform: string }; Body: DeliverBody }>(
    "/connectors/:platform/deliver",
    async (request, reply) => {
      const platform = request.params.platform;
      const connector = resolveConnector(registry, platform);
      if (!connector) return reply.code(404).send({ error: "unknown platform" });
      const { context, post } = request.body;
      request.log.info({ platform, mediaCount: post.media.length }, "deliver: start");
      try {
        const result = await connector.deliver(context, post);
        if (result?.success) {
          request.log.info({ platform, externalId: result.externalId }, "deliver: ok");
        } else {
          request.log.warn({ platform, error: result?.error }, "deliver: failed");
        }
        return result;
      } catch (err) {
        request.log.error({ err, platform }, "deliver: threw");
        return reply.code(502).send({ error: err instanceof Error ? err.message : String(err) });
      }
    },
  );

  // --- OAuth "connect" flow (platforms that expose `oauth`, e.g. Tumblr) -------------------------
  app.post<{ Params: { platform: string }; Body: { callbackUrl: string; configJson?: string } }>(
    "/connectors/:platform/oauth/request-token",
    async (request, reply) => {
      const connector = resolveConnector(registry, request.params.platform);
      if (!connector) return reply.code(404).send({ error: "unknown platform" });
      if (!connector.oauth) return reply.code(400).send({ error: "oauth not supported/configured" });
      try {
        return await connector.oauth.startAuthorization({
          callbackUrl: request.body.callbackUrl,
          configJson: request.body.configJson,
        });
      } catch (err) {
        request.log.error({ err, platform: request.params.platform }, "oauth request-token failed");
        return reply.code(502).send({ error: err instanceof Error ? err.message : String(err) });
      }
    },
  );

  app.post<{
    Params: { platform: string };
    Body: { requestToken: string; requestTokenSecret: string; verifier: string };
  }>("/connectors/:platform/oauth/access-token", async (request, reply) => {
    const connector = resolveConnector(registry, request.params.platform);
    if (!connector) return reply.code(404).send({ error: "unknown platform" });
    if (!connector.oauth) return reply.code(400).send({ error: "oauth not supported/configured" });
    try {
      return await connector.oauth.completeAuthorization(request.body);
    } catch (err) {
      request.log.error({ err, platform: request.params.platform }, "oauth access-token failed");
      return reply.code(502).send({ error: err instanceof Error ? err.message : String(err) });
    }
  });

  return app;
}
