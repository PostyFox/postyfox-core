# postyfox-core Contributors Guide

## Getting started

### Requirements

- .NET 11 SDK
- Node.js 24 and npm (for `src/connectors-node`)
- Docker or Podman (for the local stack)
- An editor: Visual Studio Code, Visual Studio 2026 or JetBrains Rider (Rider has a free
  non-commercial licence)
- The Six Labors ImageSharp licence file (`sixlabors.lic`) in the repo root for Release builds and
  images. See [ImageSharp licence](../docs/DEVELOPMENT.md#imagesharp-licence).

### Build and test

```bash
dotnet build
dotnet test
cd src/connectors-node && npm ci && npm test
```

Tests need no Docker. See [the development guide](../docs/DEVELOPMENT.md).

### Standing up a local dev stack

`deploy/docker-compose.yml` stands up the whole platform, including Keycloak, oauth2-proxy and the
nginx gateway:

```bash
cd deploy
docker compose up --build
```

Log in and exercise the APIs through the edge at http://localhost:4180 as `postyfox` / `postyfox`
(the realm import is in `deploy/keycloak`). Hitting the APIs directly (`:8080` / `:8081`) requires a
valid `Authorization: Bearer` token. Neither port is exposed on real deployments. There is no DevMode
bypass.

To run the frontend against this stack, see the postyfox-frontend README.

### Testing the API with Postman or curl

Create an API key (`POST /api/profile/keys`, or the API keys page in the frontend) and send it as
`X-API-Key`. That avoids handling Keycloak bearer tokens by hand.

### Swagger API documentation

Both APIs publish OpenAPI documents and Swagger UI. You must be signed in.

- Local: http://localhost:4180/swagger and http://localhost:4180/swagger-post
- Dev: https://dev.postyfox.com/swagger/index.html and https://dev.postyfox.com/swagger-post/index.html

### Documentation

Architecture, decisions and the endpoint reference are under [`docs/`](../docs/README.md). Update
them in the same change as the code they describe.

## Reminder

Do not commit passwords, secrets, licence files or other credentials. Use environment variables or
an untracked `.env` for local values. Secret scanning is enabled but not foolproof.

## Submitting issues

Log bugs and feature requests on the
[postyfox-core issue tracker](https://github.com/PostyFox/postyfox-core/issues). We triage them to the
right project; one issue may spawn several tickets.
