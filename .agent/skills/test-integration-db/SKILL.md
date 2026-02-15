---
name: test-integration-db
description: Database component testing - Isolated repository and EF Core testing with Testcontainers (PostgreSQL), manual migration application, concurrency testing, and SQL query validation. Use for testing data access without web overhead. Keywords: database-testing, repository-testing, testcontainers, postgresql, ef-core, concurrency-testing, migration-testing, dtt
last-updated: 2026-02-15
difficulty: intermediate
---

# Database Component Testing

> Isolated testing of Repositories and EF Core configurations using Testcontainers.

## When to Apply
Use this when you **DO NOT** need the full Web API stack (e.g., testing complicated SQL queries, triggers, or concurrency).

> [!IMPORTANT]
> Since we are not using `CreateClientAsync` (which bootstraps the app), we must **manually apply migrations**.

## Patterns

### Basic Repository Test

```csharp
[Theory]
[DataInline]
public async Task QAContext_Should_Add_Entity(DrnTestContext context)
{
    // 1. Setup Services & Migrations manually
    context.ServiceCollection.AddSampleInfraServices();
    await context.ContainerContext.Postgres.ApplyMigrationsAsync();
    
    // 2. Test Logic
    await using var qaContext = context.GetRequiredService<QAContext>();
    var category = new Category("dotnet");
    
    qaContext.Categories.Add(category);
    await qaContext.SaveChangesAsync();
    
    category.EntityId.Should().NotBe(Guid.Empty);
}
```

### Concurrency Testing
Verify optimistic concurrency by using separate service scopes to simulate conflicting users.

```csharp
[Theory]
[DataInline]
public async Task Test_Concurrency_Conflict(DrnTestContext context)
{
    context.ServiceCollection.AddSampleInfraServices();
    await context.ContainerContext.Postgres.ApplyMigrationsAsync();
    
    // Create two parallel scopes/users
    using var scope1 = context.CreateScope();
    using var scope2 = context.CreateScope();
    var ctx1 = scope1.ServiceProvider.GetRequiredService<QAContext>();
    var ctx2 = scope2.ServiceProvider.GetRequiredService<QAContext>();
    
    var cat1 = await ctx1.Categories.FindAsync(categoryId);
    var cat2 = await ctx2.Categories.FindAsync(categoryId);
    
    // User 1 Updates & Saves
    cat1!.SetExtendedProperties(new { Update = "First" });
    await ctx1.SaveChangesAsync();
    
    // User 2 Updates & Tries to Save OLD version -> Optimistic Concurrency Exception
    cat2!.SetExtendedProperties(new { Update = "Second" });
    await ctx2.Awaiting(c => c.SaveChangesAsync()).Should().ThrowAsync<DbUpdateConcurrencyException>();
}
```

## Related
- [test-integration.md](../test-integration/SKILL.md)

