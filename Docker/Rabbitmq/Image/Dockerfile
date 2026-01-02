ARG BASE_VERSION=4.2.2

FROM rabbitmq:${BASE_VERSION}-management-alpine
# https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/releases/tag/v4.2.0
ENV PLUGIN_FILE=rabbitmq_delayed_message_exchange-4.2.0.ez
COPY ${PLUGIN_FILE} $RABBITMQ_HOME/plugins/

COPY definitions.json /etc/rabbitmq/
COPY rabbitmq.conf /etc/rabbitmq/

RUN chown -R rabbitmq:rabbitmq /etc/rabbitmq/ "$RABBITMQ_HOME"/plugins/ && \
    chmod 644 /etc/rabbitmq/rabbitmq.conf /etc/rabbitmq/definitions.json && \
    rabbitmq-plugins --offline enable \
    rabbitmq_management \
    rabbitmq_delayed_message_exchange \
    rabbitmq_consistent_hash_exchange \
    rabbitmq_shovel \
    rabbitmq_shovel_management

CMD ["rabbitmq-server"]