ARG BASE_VERSION=3.12.2

FROM rabbitmq:${BASE_VERSION}-management-alpine

ADD rabbitmq_delayed_message_exchange-3.12.0.ez $RABBITMQ_HOME/plugins/rabbitmq_delayed_message_exchange-3.12.0.ez
ADD definitions.json /etc/rabbitmq/
ADD rabbitmq.conf /etc/rabbitmq/

RUN chown rabbitmq:rabbitmq /etc/rabbitmq/rabbitmq.conf /etc/rabbitmq/definitions.json $RABBITMQ_HOME/plugins/rabbitmq_delayed_message_exchange-3.12.0.ez

RUN rabbitmq-plugins enable  rabbitmq_management rabbitmq_delayed_message_exchange rabbitmq_consistent_hash_exchange rabbitmq_shovel rabbitmq_shovel_management

CMD rabbitmq-server;