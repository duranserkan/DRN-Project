---
name: test-integration-db
description: Use when testing repositories, ORM mapping, SQL queries, migrations, transactions, concurrency, or persistence behavior with a real database or container.
last-updated: 2026-06-12
difficulty: intermediate
tokens: ~0.6K
---

# Database Component Testing

> Isolated database-backed testing for repositories and persistence configuration. Apply repository-profile database and test-container conventions first.

## When to Apply

Use this when you do not need the full web/API stack: complicated queries, mapping, migrations, transactions, triggers, concurrency, or repository behavior.

## Setup Rules

- Start or bind the database through the repository's approved test fixture.
- Apply migrations or schema setup explicitly when the full application host is not used.
- Register only the infrastructure/application services needed by the component under test.
- Use separate scopes or connections when simulating concurrent users.
- Clean data deterministically between tests.

## Basic Repository Shape

```csharp
[Fact]
public async Task Repository_Should_Persist_Entity()
{
    await using var fixture = await DatabaseFixture.StartAsync();
    await fixture.ApplyMigrationsAsync();

    await using var scope = fixture.CreateScope();
    var repository = scope.GetRequiredService<IExampleRepository>();

    var entity = ExampleEntity.Create("value");
    await repository.AddAsync(entity);
    await repository.SaveChangesAsync();

    var loaded = await repository.GetAsync(entity.Id);
    loaded.Name.Should().Be("value");
}
```

## Concurrency

Verify optimistic concurrency by using separate scopes, contexts, sessions, or connections. Assert the exact conflict behavior the repository exposes, not just the ORM exception type, unless the ORM exception is the public contract.

## Related

- [test-integration](../test-integration/SKILL.md)
