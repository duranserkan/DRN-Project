---
name: test-integration-api
description: Use when testing web/API request pipelines, endpoints, routing, auth, middleware, serialization, typed HTTP clients, or outbound HTTP behavior.
last-updated: 2026-06-12
difficulty: intermediate
tokens: ~0.9K
---

# API Integration Testing

> End-to-end API testing through the repository's approved test host. Apply repository-profile conventions for authentication, fixtures, and command execution.

## Core Patterns

### Test Client

Use the local framework's test-host abstraction, such as `WebApplicationFactory<TProgram>` in ASP.NET Core, to exercise the real request pipeline.

```csharp
[Fact]
public async Task Endpoint_Should_Return_Data()
{
    await using var app = new WebApplicationFactory<Program>();
    var client = app.CreateClient();

    var response = await client.GetAsync("/api/example");

    response.EnsureSuccessStatusCode();
}
```

When the repository has an authenticated-client helper, prefer it over manually crafting cookies, claims, or tokens in each test.

### Mocking HTTP Dependencies

Mock downstream services at the transport boundary to test proxy behavior and failure modes. Keep the application pipeline real and replace only the external dependency.

```csharp
[Fact]
public async Task Endpoint_Should_Handle_Downstream_Failure()
{
    await using var app = CreateApplicationWithMockHttp(statusCode: 500);
    var client = app.CreateClient();

    var response = await client.GetAsync("/api/external-dependent");

    response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
}
```

### Route Validation

Validate route metadata without starting the full app server only when route data is the behavior under test. Otherwise, prefer an actual request so middleware, filters, serialization, and auth participate.

## Test Isolation

- Use test-specific configuration for connection strings, secrets, external URLs, and feature flags.
- Prevent tests from launching local development dependencies that would collide with containers or shared services.
- Reset database, queue, cache, or filesystem state through deterministic helpers.
- Avoid sleeping for eventual consistency; wait on observable conditions or use framework synchronization hooks.

## Related

- [test-integration](../test-integration/SKILL.md)
