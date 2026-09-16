import Config

# Closed instance: no outbound/inbound federation, no self-registration, no confirmation e-mail.
# (registrations_open/account_activation_required already default to false on a fresh instance —
# this makes it explicit and future-proof against a version bumping the defaults.) The test admin
# account is created directly by `admin-init` in docker-compose.yml, so open registration was never
# needed anyway.
config :pleroma, :instance,
  federating: false,
  registrations_open: false,
  account_activation_required: false

# Belt-and-suspenders: this stack also has no route to the real internet at all (see the
# `net: internal: true` network in docker-compose.yml), so even if federating were left on there's
# nothing for it to reach.
