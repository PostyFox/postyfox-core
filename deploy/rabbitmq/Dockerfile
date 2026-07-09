FROM rabbitmq:3.13-management
# Enables scheduled + retry-backoff delivery (x-delayed-message exchange).
ARG PLUGIN_VERSION=3.13.0
ADD https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/releases/download/v${PLUGIN_VERSION}/rabbitmq_delayed_message_exchange-${PLUGIN_VERSION}.ez /opt/rabbitmq/plugins/
# `ADD` from a URL lands the file as root-only (0600); RabbitMQ runs as the `rabbitmq` user and
# would otherwise fail to read it (invalid_ez, eacces). Make it world-readable before enabling.
RUN chmod 0644 /opt/rabbitmq/plugins/rabbitmq_delayed_message_exchange-${PLUGIN_VERSION}.ez \
 && rabbitmq-plugins enable --offline rabbitmq_delayed_message_exchange
