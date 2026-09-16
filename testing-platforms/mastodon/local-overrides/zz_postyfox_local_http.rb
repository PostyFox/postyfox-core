# Bind-mounted into config/initializers/ (see docker-compose.yml) — runs after the stock
# config/initializers/1_hosts.rb (alphabetically later), which is what actually needs overriding.
#
# Real Mastodon hardcodes `config.force_ssl = true` in config/environments/production.rb, and
# 1_hosts.rb computes `https = Rails.env.production? || ENV['LOCAL_HTTPS'] == 'true'` — so with
# RAILS_ENV=production (required for asset serving) it's always true regardless of LOCAL_HTTPS, and
# every generated URL (media/thumbnail links, mailer links, CSP, user-agent) comes out https, plus
# Rails redirects every plain-HTTP request to https — even though this stack has no TLS listener at
# all. There's no clean env-var way to flip this off under production, and unlike the hometown stack
# (built from source, see ../../hometown/fetch-source.sh) this uses upstream's prebuilt image, so we
# can't sed the source directly — add a later initializer instead. Verified against a live
# ghcr.io/mastodon/mastodon:v4.7.1 container before adding this.
Rails.application.configure do
  config.force_ssl = false
  config.x.use_https = false
  config.action_mailer.default_url_options = config.action_mailer.default_url_options.merge(protocol: 'http://')
end
