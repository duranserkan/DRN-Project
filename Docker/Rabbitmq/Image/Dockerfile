ARG BASE_VERSION=4.1.0

FROM rabbitmq:${BASE_VERSION}-management-alpine

COPY rabbitmq_delayed_message_exchange-4.1.0.ez "$RABBITMQ_HOME"/plugins/rabbitmq_delayed_message_exchange-4.1.0.ez
COPY definitions.json /etc/rabbitmq/
COPY rabbitmq.conf /etc/rabbitmq/

RUN chown rabbitmq:rabbitmq /etc/rabbitmq/rabbitmq.conf /etc/rabbitmq/definitions.json $RABBITMQ_HOME/plugins/rabbitmq_delayed_message_exchange-4.1.0.ez && \
    rabbitmq-plugins enable  \
    rabbitmq_management  \
    rabbitmq_delayed_message_exchange  \
    rabbitmq_consistent_hash_exchange  \
    rabbitmq_shovel  \
    rabbitmq_shovel_management

CMD rabbitmq-server;