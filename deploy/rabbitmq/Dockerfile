FROM rabbitmq:3.13-management
# Enables scheduled + retry-backoff delivery (x-delayed-message exchange).
ARG PLUGIN_VERSION=3.13.0
ADD https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/releases/download/v${PLUGIN_VERSION}/rabbitmq_delayed_message_exchange-${PLUGIN_VERSION}.ez /opt/rabbitmq/plugins/
RUN rabbitmq-plugins enable --offline rabbitmq_delayed_message_exchange
