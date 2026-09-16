#!/bin/sh
# Fetches (or updates) Pleroma's own source tree into ./upstream/, then patches config/docker.exs —
# see README.md. Run this once before the first `docker compose up -d`/`build`, and again any time
# you want to pick up changes on the `stable` branch (it re-clones from scratch each time, so the
# patch can never drift out of sync with upstream).
#
# Building from source at all is itself a workaround: Pleroma's own documented setup
# (https://git.pleroma.social/pleroma/pleroma-docker-compose) points at a prebuilt image,
# git.pleroma.social:5050/pleroma/pleroma:latest, but that registry now refuses connections
# outright (confirmed: TCP connection refused on port 5050, while the GitLab web UI on the same
# host is up) — it looks discontinued, not transiently down. Pleroma's own Dockerfile (in the repo
# itself, at ./upstream/Dockerfile once fetched) still works fine built locally.
set -e
cd "$(dirname "$0")"

rm -rf upstream
git clone --branch stable --depth 1 https://git.pleroma.social/pleroma/pleroma.git upstream

# config/docker.exs hardcodes `scheme: "https", port: 443` for the Phoenix Endpoint's URL-generation
# config, regardless of DOMAIN/the actual (plain-HTTP) listener — same bug family as Akkoma/Mastodon/
# Hometown had (see their fetch-source.sh / local-overrides for the same root cause explained in more
# detail). DOMAIN here is bare host only (no port), so this also switches the port to come from a
# PORT env var instead of the hardcoded 443, since there's nowhere else to source it from.
sed -i \
  -e 's/scheme: "https", port: 443/scheme: "http", port: String.to_integer(System.get_env("PORT", "4000"))/' \
  upstream/config/docker.exs

# This podman install has no registries.conf at all (not something to fix here — could be
# intentional lockdown), and unlike `podman pull`, `buildah` (used for `podman build`) refuses to
# resolve short image names ("hexpm/elixir", "alpine") without one: 'short-name ... did not resolve
# to an alias and no containers-registries.conf(5) was found'. Fully-qualifying the Dockerfile's base
# images sidesteps needing that config at all.
sed -i \
  -e 's|^ARG ELIXIR_IMG=hexpm/elixir$|ARG ELIXIR_IMG=docker.io/hexpm/elixir|' \
  -e 's|^FROM alpine:\${ALPINE_VER}$|FROM docker.io/library/alpine:${ALPINE_VER}|' \
  upstream/Dockerfile

# Pinned Erlang 26.2.5.6 can't complete `mix local.hex`/`mix deps.get`: TLS handshake to
# builds.hex.pm/repo.hex.pm fails with {key_usage_mismatch, ...} — a real, documented OTP bug
# (erlang/otp#9286, hexpm/hex#1175) from a hex.pm CDN cert rotation combined with overly-strict
# X.509 key-usage validation in affected OTP releases; fixed in OTP 27.2+. Bumping to the newest
# available hexpm/elixir Alpine tag for this Elixir version, which carries a patched OTP.
sed -i \
  -e 's/^ARG ERLANG_VER=26.2.5.6$/ARG ERLANG_VER=27.3.4.17/' \
  -e 's/^ARG ALPINE_VER=3.17.9$/ARG ALPINE_VER=3.21.7/' \
  upstream/Dockerfile

echo "upstream/ ready at $(git -C upstream rev-parse --short HEAD)"
