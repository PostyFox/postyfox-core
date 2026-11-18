# PostyFox Core

PostyFox lets you write a post once and send it to many social platforms, now or on a schedule. This
repository is the backend: the APIs, the posting pipeline that delivers to each platform, and
everything needed to run it. The web app lives in
[postyfox-frontend](https://github.com/PostyFox/postyfox-frontend).

## What it does

- Connect your accounts once, then post to any mix of them in one go.
- Tailor each post per platform: templates, tag presets, content warnings, ratings and each site's
  own submission options.
- Attach images and video. PostyFox resizes or converts them to fit each platform's limits.
- Schedule posts, save drafts, and repost or delete automatically after a set time.
- See what was delivered where, cancel what hasn't gone yet, and send a past post again.
- Let someone else manage your account without sharing your login.
- Trigger posts automatically from signed webhooks.
- Script it all with API keys.

## Supported platforms

Discord, Telegram, Bluesky, Tumblr, Instagram, the Fediverse (Mastodon, Pleroma, Akkoma, Friendica,
Iceshrimp, GoToSocial, Hometown, Pixelfed), FurAffinity, Toyhouse, Ko-fi, SoFurry, DeviantArt,
Artconomy and X.

Sites with no posting API connect through the [PostyFox Connect](./clients/postyfox-connect/README.md)
browser extension, which hands over your logged-in session. Automated posting to X is against X's
terms of service and can get the account suspended.

## Quick start

You need Docker or Podman, and the Six Labors licence file (`sixlabors.lic`) in the repo root (see
[ImageSharp licence](./docs/DEVELOPMENT.md#imagesharp-licence)).

```bash
cd deploy
docker compose up --build
```

Open <http://localhost:4180> and sign in as `postyfox` / `postyfox`. To use the web app as well, see
the frontend README.

## Build and test

```bash
dotnet build
dotnet test
cd src/connectors-node && npm ci && npm test
```

Requires the .NET 11 SDK and Node.js 24. Tests need no Docker.

## How it's built

C# (ASP.NET Core) APIs and a worker, plus a Node service for most platform connectors, with
PostgreSQL, RabbitMQ and S3-compatible storage behind them. Login goes through Keycloak. It runs on
docker-compose or Kubernetes.

## Documentation

| Doc | For |
|-----|-----|
| [docs/DEVELOPMENT.md](./docs/DEVELOPMENT.md) | Running locally, building, configuration, the ImageSharp licence |
| [docs/ARCHITECTURE.md](./docs/ARCHITECTURE.md) | How the system fits together, with diagrams |
| [docs/DECISIONS.md](./docs/DECISIONS.md) | Why it's built this way (ADRs) |
| [docs/README.md](./docs/README.md) | Doc index and HTTP endpoint reference |
| [docs/oauth-connectors.md](./docs/oauth-connectors.md) | Setting up the OAuth platforms |
| [deploy/README.md](./deploy/README.md) | Deploying to dev and production |
| [docs/FOLLOWUPS.md](./docs/FOLLOWUPS.md) | Known gaps and deferred work |

## Contributing

See [CONTRIBUTING](./.github/CONTRIBUTING.md). Report security issues privately as described in
[SECURITY](./.github/SECURITY.md).

## Releases

Current (production ready) releases can be found here on GitHub.
In development releases can be found on https://packages.neillans.co.uk, however, you should ONLY use these if you know what you are doing or have been asked to test something specific!

The packages server (packages.neillans.co.uk) is available through the great generosity of Inedo by providing us with an Enterprise License for our Open Source projects.