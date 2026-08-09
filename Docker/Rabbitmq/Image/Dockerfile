# Version and digest arguments are coupled: BASE_VERSION must match BASE_DIGEST.
ARG BASE_VERSION=4.3.4
ARG BASE_DIGEST=sha256:44bf7eb50fe1765885659e49ccfdc775f8e531964d979321aee380a071f49f94

FROM rabbitmq:${BASE_VERSION}-management-alpine@${BASE_DIGEST}

COPY definitions.json /etc/rabbitmq/
COPY rabbitmq.conf /etc/rabbitmq/

# rabbitmq_delayed_message_exchange is intentionally not bundled. Team RabbitMQ archived the plugin,
# stopped maintaining it, and notes it depends on Mnesia, which was removed from RabbitMQ 4.3.
# See: https://github.com/rabbitmq/rabbitmq-delayed-message-exchange
RUN chown -R rabbitmq:rabbitmq /etc/rabbitmq/ "$RABBITMQ_HOME"/plugins/ && \
    chmod 644 /etc/rabbitmq/rabbitmq.conf /etc/rabbitmq/definitions.json && \
    rabbitmq-plugins --offline enable \
    rabbitmq_management \
    rabbitmq_consistent_hash_exchange \
    rabbitmq_shovel \
    rabbitmq_shovel_management

CMD ["rabbitmq-server"]
