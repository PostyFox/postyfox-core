#!/bin/sh
# Fetches (or updates) the Hometown source tree into ./upstream/, then applies two local-test-stack
# patches — see README.md. Run this once before the first `docker compose up -d`/`build`, and again
# any time you want to pick up changes on the `hometown-dev` branch (it re-clones from scratch each
# time, so neither patch can drift out of sync with upstream).
set -e
cd "$(dirname "$0")"

rm -rf upstream
git clone --branch hometown-dev --depth 1 https://github.com/hometown-fork/hometown.git upstream

# podman/buildah's Dockerfile parser errors with "reading multiple stages: attempted to redefine
# \"TARGETPLATFORM\": invalid argument" on a global `ARG NAME=${NAME}` (self-referential default)
# declared before any FROM in a multi-stage build. Both Dockerfiles here (the main app and the
# separate streaming-server one) have two such lines each, written purely for buildx clarity
# (automatic platform args are already implicit pre-FROM either way) — a bare `ARG NAME` behaves
# identically under both buildx and buildah and avoids the bug.
sed -i \
  -e 's/^ARG TARGETPLATFORM=\${TARGETPLATFORM}$/ARG TARGETPLATFORM/' \
  -e 's/^ARG BUILDPLATFORM=\${BUILDPLATFORM}$/ARG BUILDPLATFORM/' \
  upstream/Dockerfile upstream/streaming/Dockerfile

# config.force_ssl = true is unconditional in production.rb — real deployments sit behind a
# TLS-terminating reverse proxy, but this stack serves plain HTTP directly, so it just makes Rails
# redirect every request to https:// (which nothing here listens on) and set secure-only cookies.
# There's no env var to toggle this upstream, so patch it off directly.
sed -i \
  -e 's/^  config.force_ssl = true$/  config.force_ssl = false/' \
  upstream/config/environments/production.rb

# 1_hosts.rb hardcodes `https = Rails.env.production? || ENV['LOCAL_HTTPS'] == 'true'` — LOCAL_HTTPS
# can only force https ON in non-production, never OFF in production, so with RAILS_ENV=production
# (required for asset precompilation) every generated absolute URL (streaming_api, media/thumbnail
# links, mailer links) comes out https/wss regardless of force_ssl above — same symptom as Akkoma's
# bug (browsers attempt a real TLS handshake against a plain-HTTP port). Make LOCAL_HTTPS the sole
# switch so `.env`'s `LOCAL_HTTPS=false` actually takes effect.
sed -i \
  -e "s/^  https = Rails.env.production? || ENV\['LOCAL_HTTPS'\] == 'true'\$/  https = ENV['LOCAL_HTTPS'] == 'true'/" \
  upstream/config/initializers/1_hosts.rb

echo "upstream/ ready at $(git -C upstream rev-parse --short HEAD)"
