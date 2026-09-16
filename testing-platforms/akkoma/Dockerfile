# Builds a local Akkoma image from source (Akkoma publishes no prebuilt image — see
# https://docs.akkoma.dev/stable/installation/docker_en/). Pinned to the `stable` branch. Config is
# generated at build time with `mix pleroma.instance gen` (Akkoma is a Pleroma fork and keeps the
# same `pleroma.*` mix tasks) using fixed, test-only values, then federation/registration/e-mail-
# confirmation are turned off by appending an override block — see README.md.
FROM hexpm/elixir:1.15.4-erlang-26.0.2-alpine-3.18.2

ENV MIX_ENV=prod
ENV ERL_EPMD_ADDRESS=127.0.0.1

RUN apk add --no-cache git gcc g++ musl-dev make cmake file-dev exiftool ffmpeg imagemagick libmagic ncurses postgresql-client unzip

WORKDIR /opt/akkoma

RUN git clone --branch stable --depth 1 https://akkoma.dev/AkkomaGang/akkoma.git .

RUN mix local.hex --force && mix local.rebar --force
RUN mix deps.get
RUN mix compile

RUN mix pleroma.instance gen --force \
    --domain "localhost:4020" \
    --media-url "" \
    --instance-name "PostyFox Test Akkoma" \
    --admin-email "admin@postyfox.test" \
    --notify-email "admin@postyfox.test" \
    --dbhost "db" \
    --dbname "akkoma" \
    --dbuser "akkoma" \
    --dbpass "akkoma-test-pw" \
    --rum n \
    --indexable n \
    --db-configurable n \
    --listen-ip "0.0.0.0" \
    --listen-port 4000 \
    --uploads-dir "uploads" \
    --static-dir "instance/static/" \
    --strip-uploads-metadata n \
    --read-uploads-description n \
    --anonymize-uploads n \
    --output config/prod.secret.exs \
    --output-psql config/setup_db.psql \
 && sed -i 's/scheme: "https"/scheme: "http"/' config/prod.secret.exs \
 && printf '\n# --- PostyFox test-stack overrides: closed instance, see testing-platform/README.md ---\nconfig :pleroma, :instance,\n  federating: false,\n  registrations_open: false,\n  account_activation_required: false\n' >> config/prod.secret.exs

# Default config already sets pleroma-fe as the primary frontend and admin-fe as the admin one —
# they just aren't installed yet, so the app serves its built-in "no frontend installed" page.
# `mix pleroma.frontend install` (the normal way) boots the full app incl. the Ecto repo, which
# needs a live `db` — not available at build time — so fetch and unpack the same release bundles
# directly instead, straight into the paths install_frontend would have written them to.
RUN mkdir -p /tmp/frontends && \
    wget -q -O /tmp/frontends/pleroma-fe.zip "https://akkoma-updates.s3-website.fr-par.scw.cloud/frontend/stable/akkoma-fe.zip" && \
    unzip -q /tmp/frontends/pleroma-fe.zip -d /tmp/frontends/pleroma-fe && \
    mkdir -p instance/static/frontends/pleroma-fe/stable && \
    cp -r /tmp/frontends/pleroma-fe/dist/. instance/static/frontends/pleroma-fe/stable/ && \
    wget -q -O /tmp/frontends/admin-fe.zip "https://akkoma-updates.s3-website.fr-par.scw.cloud/frontend/stable/admin-fe.zip" && \
    unzip -q /tmp/frontends/admin-fe.zip -d /tmp/frontends/admin-fe && \
    mkdir -p instance/static/frontends/admin-fe/stable && \
    cp -r /tmp/frontends/admin-fe/dist/. instance/static/frontends/admin-fe/stable/ && \
    rm -rf /tmp/frontends

EXPOSE 4000

CMD ["mix", "phx.server"]
