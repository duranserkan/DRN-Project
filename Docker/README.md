# Docker and Docker Compose

This guide covers local containers, Docker Compose stacks, existing data volumes, and published image verification for [DRN-Project](../README.md).

## Local development

Install [Docker](https://www.docker.com/) and keep its daemon running. Docker Compose stacks require Compose 2.30 or newer.

When running the sample with `dotnet run --project Sample.Hosted` from the repository root, its default Debug/Development startup provisions PostgreSQL through Testcontainers on host port **6432**. Keep that port available. The sample runs at http://localhost:5998.

### Start the application stack

For a fresh local stack, run these commands from the repository root:

```bash
cp -n .sample-env .env
docker compose up -d --build
```

The [root Compose stack](../docker-compose.yml) runs `Sample.Hosted` with PostgreSQL, RabbitMQ, and Graylog. The template includes working, public local development credentials and keys. No manual secret generation is needed for disposable local data.

The copy command preserves an existing `.env`. Existing installations should edit their current file and follow the [migration guidance](#existing-data-and-migration).

### Start standalone dependencies

Choose the directory for the dependencies you need:

| Directory | Services |
|---|---|
| [Docker/Postgre](Postgre/docker-compose.yml) | PostgreSQL |
| [Docker/Rabbitmq](Rabbitmq/docker-compose.yml) | RabbitMQ with read-only configuration mounts |
| [Docker/Graylog](Graylog/docker-compose.yml) | Graylog and its MongoDB and Data Node dependencies |
| [Docker/Infra](Infra/docker-compose.yml) | PostgreSQL, RabbitMQ, and Graylog with its dependencies |

Run these commands from the selected directory:

```bash
cp -n .sample-env .env
docker compose up -d
```

Choose one stack for each dependency because the stacks share host ports.

### Local access

| Service | Local address | Username | Password |
|---|---|---|---|
| Sample | http://localhost:5998 | Register through the app | Choose during registration |
| PostgreSQL | localhost:5432, database `drn` | `drn` | `drn` |
| RabbitMQ management | http://localhost:15672 | `drn` | `drn` |
| Graylog | http://localhost:9000 | `admin` | `drn` |

Graylog requires its initial Data Node setup through the preflight UI. Use the temporary credentials shown in its container logs for that step. The configured `admin` / `drn` login applies afterward. See [Graylog's Docker setup instructions](https://go2docs.graylog.org/current/downloading_and_installing_graylog/docker_installation.htm).

## Credentials and network access

The public credentials and encryption keys are only for local development with disposable data. Before using a shared deployment, replace them with unique secrets:

- Generate a Nexus key with `openssl rand -hex 32` and keep `NexusAppSettings__Keys__0__Format=Hex`.
- Generate `DrnAppFeatures__SeedKey` independently. Use 58-256 ASCII characters with uppercase, lowercase, digits, and at least one of `!*()-_`, with no more than four sequential or three identical characters in a row.
- Preserve both keys across restarts.

The application still rejects its built-in staging example keys. The local template supplies separate values that satisfy validation.

All published ports in the root and standalone dependency Compose stacks bind to `127.0.0.1`, including the Sample app, PostgreSQL, RabbitMQ, and Graylog. Container-to-container connections use service names and container ports. Remote access requires an explicit deployment override with appropriate authentication and transport protection.

The root stack serves Sample over HTTP at http://localhost:5998. It does not publish port 5999 or mount a TLS certificate. For HTTPS, configure a separate deployment override with an explicit Kestrel HTTPS endpoint, certificate settings, and the corresponding port mapping.

## Existing data and migration

For new local databases, the templates provide PostgreSQL and RabbitMQ password `drn`, a fixed Graylog secret, the hash of Graylog password `drn`, and RabbitMQ hostname `drn-rabbit`. Compose rejects missing required values if the template has not been copied or a required value is cleared.

For existing databases, edit the existing `.env` rather than replacing it with the template. Configuration changes do not automatically migrate stored credentials:

- **PostgreSQL:** Changing `POSTGRES_PASSWORD` in `.env` does not rotate the stored password. Rotate it in PostgreSQL and update clients together.
- **RabbitMQ:** Preserve the broker's original hostname and follow the [RabbitMQ setup and migration instructions](Rabbitmq/README.md) before reusing an existing volume.
- **Graylog:** Change the root password through `GRAYLOG_ROOT_PASSWORD_SHA2`, but preserve the existing `GRAYLOG_PASSWORD_SECRET`. Changing that secret can invalidate sessions and encrypted data.

The root, Infra, and standalone PostgreSQL stacks fix `PGDATA` to `/data/postgres`, matching their `drn-postgres-data` volume mount. A `PGDATA` value in `.env` no longer overrides this path. Existing databases using the default path need no migration. If you previously used another path, back up that database and migrate it to the named volume before recreating the container. Changing the configuration does not move existing data.

## Verify Published Container Images

Stable and preview releases publish provenance for each multi-platform image index and a separate SPDX 2.3 SBOM attestation for each `linux/amd64` and `linux/arm64` manifest. Separate platform bindings prevent one architecture's inventory from representing another. Replace `RELEASE_VERSION`, inspect the image, then replace `PLATFORM_MANIFEST_HEX_DIGEST` with the hexadecimal part of the corresponding platform digest (for preview images, replace `release.yml` with `release-preview.yml`):

```bash
docker buildx imagetools inspect docker.io/duranserkan/drn-project-sample:RELEASE_VERSION

gh attestation verify \
  oci://index.docker.io/duranserkan/drn-project-sample:RELEASE_VERSION \
  -R duranserkan/DRN-Project \
  --signer-workflow duranserkan/DRN-Project/.github/workflows/release.yml

gh attestation verify \
  oci://index.docker.io/duranserkan/drn-project-sample@sha256:PLATFORM_MANIFEST_HEX_DIGEST \
  -R duranserkan/DRN-Project \
  --signer-workflow duranserkan/DRN-Project/.github/workflows/release.yml \
  --predicate-type https://spdx.dev/Document/v2.3
```

Replace `drn-project-sample` with `drn-project-nexus` to verify the Nexus image.
