import { buildServer } from "./server.js";

const port = Number(process.env.PORT ?? 8090);
const host = "0.0.0.0";

const internalToken = process.env.INTERNAL_TOKEN;
if (!internalToken) {
  console.error("INTERNAL_TOKEN is not set — refusing to start.");
  process.exit(1);
}

const app = buildServer({
  internalToken,
  logger: true,
});

app
  .listen({ port, host })
  .then((address) => {
    app.log.info(`connectors-node listening on ${address}`);
  })
  .catch((err) => {
    app.log.error(err);
    process.exit(1);
  });
