FROM rabbitmq:4.3.5-management
# rabbitmq_prometheus (bundled) exposes metrics on :15692 for the otel-collector to scrape.
# Delayed scheduling/retry no longer uses rabbitmq_delayed_message_exchange (unmaintained, Mnesia-
# based, incompatible with RabbitMQ 4.3+): post scheduling is now driven by PostSchedulerService
# polling the database, and retry backoff uses a plain per-message-TTL + dead-letter-exchange queue —
# see PostyFox.Infrastructure/Messaging/Topology.cs.
RUN rabbitmq-plugins enable --offline rabbitmq_prometheus
