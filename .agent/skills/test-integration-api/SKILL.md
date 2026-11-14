---
name: test-integration-api
description: Use when testing web/API request pipelines, endpoints, routing, auth, middleware, serialization, typed HTTP clients, or outbound HTTP behavior.
last-updated: 2026-11-13
difficulty: intermediate
---

# API Integration Testing

Exercise the request pipeline through the repository's approved test host. Apply profile conventions for authentication, fixtures, and execution.

## Core Patterns

### Test Client

Use the local framework's test-host abstraction, such as `WebApplicationFactory<TProgram>` in ASP.NET Core, to exercise the real request pipeline.

Assert the endpoint's exact status, response shape, and relevant headers. A successful status alone does not prove the contract.

When the repository has an authenticated-client helper, prefer it over manually crafting cookies, claims, or tokens in each test.

### Mocking HTTP Dependencies

Mock downstream services at the transport boundary to test proxy behavior and failure modes. Keep the application pipeline real and replace only the external dependency.

Configure transport failures through the repository fixture. Assert the application's documented failure response, rather than assuming every downstream error becomes HTTP 502.

### Route Validation

Validate route metadata without starting the full app server only when route data is the behavior under test. Otherwise, prefer an actual request so middleware, filters, serialization, and auth participate.

## Test Isolation

- Use test-specific configuration for connection strings, secrets, external URLs, and feature flags.
- Prevent tests from launching local development dependencies that would collide with containers or shared services.
- Reset database, queue, cache, or filesystem state through deterministic helpers.
- Wait on observable conditions or framework synchronization hooks instead of fixed sleeps.

## Composed Request Decisions

Apply [MC/DC fixture rules](../test-unit/SKILL.md#composed-decisions) to authorization, validation, tenant, middleware, and feature-gate decisions. Pair requests and assert exact status and response membership. Cover distinct pipeline interactions.

## Related

- [test-integration](../test-integration/SKILL.md)
