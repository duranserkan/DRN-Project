# RabbitMQ containers

The standalone, Infra, and root Compose stacks use the official `rabbitmq:4.3.6-management-alpine` image directly, without a custom build.
The Compose image and Testing package default use the multi-platform digest
`sha256:3ef7f7e89fed709a78f79b8b5f4e30e1312a1d4089da752e23595566d9d61a95`.
This digest was verified against [Docker Hub tag metadata](https://hub.docker.com/v2/repositories/library/rabbitmq/tags/4.3.6-management-alpine) on 2026-10-14, including Linux AMD64 and ARM64 entries.

## Credentials and access

For a fresh standalone local broker, run from `Docker/Rabbitmq`:

```bash
cp -n .sample-env .env
docker compose up -d
```

The template supplies public local credentials `drn` / `drn` and hostname `drn-rabbit`. Open http://localhost:15672 for management. Root and Infra templates supply the same defaults. Use these credentials only for local development and replace the password before any shared deployment. The copy command preserves existing `.env` files; existing databases need the migration steps below. Compose rejects empty credentials. The mounted definitions contain no users or passwords.

For a new database, retain the template's `RABBITMQ_HOSTNAME=drn-rabbit` for the lifetime of the volume. Compose assigns it as the container hostname, so the default node name remains `rabbit@<hostname>` across recreation. For an existing database, follow the migration steps below instead of adopting the template's hostname. Compose rejects an empty hostname to prevent an unnoticed node identity change.

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

Before removing or recreating the old container, record its hostname with `docker inspect --format '{{.Config.Hostname}}' drn-rabbit`. Use `drn-infra-rabbit` for the root or Infra stack. Set `RABBITMQ_HOSTNAME` in that stack's existing `.env` to the exact returned hostname, even if it looks like a container ID. Keep the same Compose project name and volume. If the deployment overrides `RABBITMQ_NODENAME` or uses long node names, preserve those settings too. If the old container is already gone, recover the original node identity from a backup or the stored database metadata before starting the replacement. Do not guess a new hostname for a populated volume.

## Plugins

The mounted `enabled_plugins` file selects management, Prometheus, consistent-hash exchange, Shovel, and Shovel management. All are bundled in the official image. The file replaces the image's plugin selection, so management and Prometheus are explicitly retained. See the [official plugin list](https://www.rabbitmq.com/docs/plugins).

The [archived delayed-message exchange plugin](https://github.com/rabbitmq/rabbitmq-delayed-message-exchange) is not downloaded or enabled. It depends on Mnesia, which RabbitMQ 4.3 removed. Existing applications using `x-delayed-message` must migrate their topology before upgrading.

## Changes

- Replaced the custom image build with the official digest-pinned 4.3.6 image and read-only configuration mounts.
- Removed baked-in administrator definitions and remote guest access.
- Required explicit bootstrap credentials and restricted published ports to localhost in all three Compose stacks.
- Added a credential-free example topology imported by Compose after broker initialization.
- Wait for the explicitly configured broker PID file before importing definitions to avoid an early startup RPC race.
- Require a stable hostname and preserve the original hostname when migrating existing data volumes.
