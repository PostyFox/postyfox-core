import { buildServer } from "./server.js";

const port = Number(process.env.PORT ?? 8090);
const host = "0.0.0.0";

const app = buildServer({
  internalToken: process.env.INTERNAL_TOKEN,
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
