# RabbitMQ containers

The standalone, Infra, and root Compose stacks use the official `rabbitmq:4.3.6-management-alpine` image directly, without a custom build.
The Compose image and Testing package default use the multi-platform digest
`sha256:3ef7f7e89fed709a78f79b8b5f4e30e1312a1d4089da752e23595566d9d61a95`.
This digest was verified against [Docker Hub tag metadata](https://hub.docker.com/v2/repositories/library/rabbitmq/tags/4.3.6-management-alpine) on 2026-10-14, including Linux AMD64 and ARM64 entries.

## Credentials and access

Copy the relevant `.sample-env` to `.env`. Set `RABBITMQ_DEFAULT_USER` to a non-guest username and `RABBITMQ_DEFAULT_PASS` to a unique generated password before starting the stack. Compose rejects empty credentials. The mounted definitions contain no users or passwords.

Each stack mounts `Image/enabled_plugins`, `Image/definitions.json`, and `Image/rabbitmq.conf` read-only at their RabbitMQ configuration paths. The configuration fragment is mounted as `/etc/rabbitmq/conf.d/90-drn.conf` to override the official image's guest access default. Keep these files readable by the container's `rabbitmq` user. The remaining image configuration stays intact.

Host ports 5672 and 15672 bind only to `127.0.0.1`. Other services on the Compose network connect to `rabbit:5672` with the configured credentials. External access requires an explicit deployment configuration with appropriate authentication and transport protection.

## Automatic example import

Use Docker Compose 2.30 or newer. All three Compose stacks set `RABBITMQ_PID_FILE=/var/lib/rabbitmq/rabbitmq.pid` and run ordered `post_start` hooks as the `rabbitmq` user. The first uses `rabbitmqctl wait` with that same PID-file path and `--timeout 120`. It waits for the PID file, Erlang connectivity, and application startup, including when the hook starts before the broker process is reachable. The second imports `/etc/rabbitmq/definitions.json` with a 120-second timeout. CLI failures are reported by Compose. The broker can already accept connections while the hook runs, so clients outside the Compose startup sequence must not assume that an open port means the example is ready.

The file declares a durable direct exchange and durable classic queue named `drn.example` in the default `/` vhost, bound with routing key `example`. It contains no users, password hashes, permissions, or vhosts. Environment-based bootstrap creates the administrator and default vhost before the import. Do not enable native boot-time definitions import, since that suppresses bootstrap-user creation.

Repeated imports preserve matching topology and do not purge messages. Changing an existing queue or exchange to incompatible properties can fail the import. Removing an item from the JSON does not delete it from RabbitMQ. After editing the mounted files, recreate the service through Compose to apply configuration and run the import hooks. No image rebuild is needed.

These hooks are Compose-managed. Running the image directly or restarting it through Docker's automatic restart policy does not invoke them. Imported durable topology remains in the existing data volume. Broker health checks indicate broker availability, not successful definitions import. After an import failure, correct the definitions and recreate the service through Compose before relying on the example topology.

## Existing data volumes

Bootstrap environment variables only create credentials for a new database. Recreating the container does not rotate or delete users in an existing RabbitMQ data volume.

Before using an existing volume, back it up and use the current administrator account to create or rotate the intended administrator credentials through the local management UI. Update clients and verify their access, then remove the old `guest` account and any unused legacy administrator accounts. If retaining `drn`, rotate its password. The mounted configuration restores the guest loopback restriction, so guest logins through Docker port forwarding are rejected. If guest is the only available administrator, use `rabbitmqctl` inside the running broker to establish a replacement account before deleting it.

Preserve the data volume and existing node identity during migration. Do not delete volumes to apply this change. Stored users are not migrated automatically.

## Plugins

The mounted `enabled_plugins` file selects management, Prometheus, consistent-hash exchange, Shovel, and Shovel management. All are bundled in the official image. The file replaces the image's plugin selection, so management and Prometheus are explicitly retained. See the [official plugin list](https://www.rabbitmq.com/docs/plugins).

The [archived delayed-message exchange plugin](https://github.com/rabbitmq/rabbitmq-delayed-message-exchange) is not downloaded or enabled. It depends on Mnesia, which RabbitMQ 4.3 removed. Existing applications using `x-delayed-message` must migrate their topology before upgrading.

## Changes

- Replaced the custom image build with the official digest-pinned 4.3.6 image and read-only configuration mounts.
- Removed baked-in administrator definitions and remote guest access.
- Required explicit bootstrap credentials and restricted published ports to localhost in all three Compose stacks.
- Added a credential-free example topology imported by Compose after broker initialization.
- Wait for the explicitly configured broker PID file before importing definitions to avoid an early startup RPC race.
