## DRN.Framework Expert

> Domain expertise for DRN.Framework—a convention-based .NET framework emphasizing attribute-driven DI, secure hosting, source-known IDs, and comprehensive testing.

### When to Apply
- Developing new features within DRN.Framework projects
- Extending framework capabilities
- Debugging framework-related issues  
- Reviewing code that uses DRN.Framework conventions
- Testing applications built on DRN.Framework

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                     DRN.Framework Stack                         │
├─────────────────────────────────────────────────────────────────┤
│  DRN.Framework.Hosting      │  Web hosting, security, endpoints │
│  DRN.Framework.Testing      │  Test contexts, containers        │
│  DRN.Framework.EntityFramework │ DbContext, migrations, IDs     │
│  DRN.Framework.SharedKernel │  Domain, exceptions, JSON         │
│  DRN.Framework.Utils        │  DI, settings, logging, IDs       │
└─────────────────────────────────────────────────────────────────┘
```

---

## Core Conventions

### 1. Attribute-Based Dependency Injection

DRN uses assembly scanning to register services automatically via attributes.

| Attribute | Lifetime | Notes |
|-----------|----------|-------|
| `[Singleton<TService>]` | Singleton | `TryAdd` by default |
| `[Scoped<TService>]` | Scoped | Most common for request-scoped |
| `[Transient<TService>]` | Transient | New instance per resolution |
| `[HostedService]` | Singleton (IHostedService) | Background services |
| `[Config]` | Singleton | Binds configuration section to class |

**Registration triggered by**:
```csharp
services.AddServicesWithAttributes();  // Scans calling assembly
```

**Key Pattern**: Classes must be `public`, non-abstract, with the attribute to be discovered.

---

### 2. Configuration Binding with [Config]

```csharp
[Config("SectionName")]  // Binds appsettings:SectionName
public class MySettings
{
    public string ApiKey { get; set; }
    public int Timeout { get; set; }
}

[Config]  // Uses class name as section key
public class FeatureFlags { ... }

[ConfigRoot]  // Binds to configuration root
public class RootSettings { ... }
```

**Validation**: Data annotations validated on construction. Throws `ConfigurationException` on unknown keys.

---

### 3. DrnProgramBase Hosting Pattern

All DRN web applications inherit from `DrnProgramBase<TProgram>`:

```csharp
public class Program : DrnProgramBase<Program>, IDrnProgram
{
    public static Task Main(string[] args) => RunAsync(args);

    protected override Task AddServicesAsync(
        WebApplicationBuilder builder, 
        IAppSettings appSettings, 
        IScopedLog scopedLog)
    {
        builder.Services.AddDrnUtils();
        builder.Services.AddServicesWithAttributes();
        return Task.CompletedTask;
    }
}
```

**Built-in Security**:
- CSP with nonce-based script protection
- Security headers (HSTS, X-Frame-Options, etc.)
- Cookie policy (SameSite=Strict, Secure)
- MFA enforcement via `[Authorize(Policy = AuthPolicy.Mfa)]`
- Host filtering, forwarded headers

**Pipeline Hooks** (override for customization):
- `ConfigureApplicationPipelineStart()` — Before routing
- `ConfigureApplicationPreScopeStart()` — Static files
- `ConfigureApplicationPostAuthentication()` — MFA middleware
- `MapApplicationEndpoints()` — Controllers & Razor pages

---

### 4. Endpoint Management

Type-safe endpoint references via `EndpointCollectionBase<TProgram>`:

```csharp
public class Endpoints : EndpointCollectionBase<Program>
{
    public UserEndpoints User { get; } = new();
}

public class UserEndpoints
{
    public EndpointFor<UserController> GetProfile { get; } = new(c => c.GetProfile);
    public EndpointFor<UserController> UpdateProfile { get; } = new(c => c.UpdateProfile);
}
```

**Usage**: Generate URLs, validate routes at startup, prevent broken links.

---

### 5. Source-Known Entity IDs

DRN uses a custom ID scheme encoding: **timestamp + appId + appInstanceId + sequenceId**.

```csharp
[EntityType(1)]  // Unique byte per entity type
public class User : SourceKnownEntity
{
    public string Username { get; set; }
}
```

**Benefits**:
- IDs are globally unique across distributed systems
- Creation timestamp embedded in ID
- App and instance traceable from ID
- ~2M IDs/second per instance capacity

**Key Interfaces**:
- `ISourceKnownIdUtils.Next<TEntity>()` — Generate new ID
- `SourceKnownId.Parse()` — Extract metadata from ID

---

### 6. DrnContext (Entity Framework)

Convention-based DbContext with automatic configuration:

```csharp
[DrnContextServiceRegistration, DrnContextDefaults]
public class AppDbContext : DrnContext<AppDbContext>
{
    public DbSet<User> Users { get; set; }

    protected AppDbContext(DbContextOptions<AppDbContext> options) 
        : base(options) { }
    
    public AppDbContext() { }  // Required for migrations
}
```

**Automatic Features**:
- Connection string by convention: `ConnectionStrings:AppDbContext`
- Materialization interceptor for ID parsing
- SaveChanges interceptor for ID generation & ModifiedAt
- Validation of `[EntityType]` uniqueness at startup
- Auto-migration in development mode

**Migrations**:
```bash
dotnet ef migrations add --context AppDbContext MigrationName
dotnet ef database update --context AppDbContext -- "connectionString"
```

---

### 7. Scoped Logging

Request-scoped structured logging via `IScopedLog`:

```csharp
public class MyService(IScopedLog scopedLog)
{
    public void DoWork()
    {
        scopedLog.Add("Operation", "DoWork");
        scopedLog.AddToActions("Started processing");
        
        using var duration = scopedLog.Measure("ProcessingTime");
        // work...
    }
}
```

**Captured automatically by HttpScopeMiddleware**:
- TraceId, Request (path, method, host, IP)
- Response (status code, content length)
- Exception details with inner exceptions
- Scope duration

---

### 8. ScopeContext (Ambient Context)

Access scoped data anywhere via `ScopeContext`:

```csharp
// In any class
var userId = ScopeContext.UserId;
var isAdmin = ScopeContext.IsUserInRole("Admin");
var flag = ScopeContext.IsClaimFlagEnabled("FeatureX");
ScopeContext.Log.Add("Custom", "value");
```

**Available Data**: User, Log, Settings, TraceId, Services, Data dictionary.

---

### 9. Exception Handling

DRN exceptions map to HTTP status codes:

| Exception | Status Code |
|-----------|-------------|
| `ValidationException` | 400 |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| `ExpiredException` | 410 |
| `UnprocessableEntityException` | 422 |
| `ConfigurationException` | 500 |
| `MaliciousRequestException` | Abort |

**Usage**: `throw ExceptionFor.NotFound("User not found");`

---

## Testing Framework

### DrnTestContext

Slim service collection for unit/integration tests:

```csharp
[Theory]
[DataInlineUnit]
public void MyTest(DrnTestContextUnit context, IMockable mock)
{
    context.ServiceCollection.AddMyServices();
    mock.DoSomething().Returns(42);
    
    var service = context.GetRequiredService<MyService>();
    service.Execute().Should().Be(42);
}
```

**Key Features**:
- Auto-mocking with NSubstitute via `DataInlineUnit`
- Auto-fixture for data generation
- Scoped service provider per test
- Configuration building with in-memory sources

### ApplicationContext

Integration testing with `WebApplicationFactory`:

```csharp
[Theory]
[DataInline]
public async Task IntegrationTest(DrnTestContext context)
{
    context.ApplicationContext.LogToTestOutput(output);
    var client = await context.ApplicationContext.CreateClientAsync<Program>();
    
    var response = await client.GetAsync("/api/users");
    response.Should().BeSuccessful();
}
```

### ContainerContext

Testcontainers integration for databases:

```csharp
[Collection(PostgresCollection.Name)]
public class DbTests
{
    [Theory]
    [DataInline]
    public async Task Test(DrnTestContext context)
    {
        await context.ContainerContext.Postgres.ApplyMigrationsAsync();
        // Database is now ready with migrations applied
    }
}
```

### Test Startup Jobs

Run once-per-assembly setup:

```csharp
public class MyStartupJob : ITestStartupJob
{
    public async Task RunAsync(StartupContext context)
    {
        // Initialize shared resources
    }
}
```

---

## Key Interfaces Reference

| Interface | Purpose | Lifetime |
|-----------|---------|----------|
| `IAppSettings` | Typed configuration access | Singleton |
| `IScopedLog` | Request-scoped structured logging | Scoped |
| `IScopedUser` | Current user claims/identity | Scoped |
| `ISourceKnownIdUtils` | ID generation for entities | Singleton |
| `IEpochTimeUtils` | Time calculations from epoch | Singleton |
| `IDrnContext` | EF Core DbContext contract | Scoped |
| `IEndpointAccessor` | Runtime endpoint metadata | Singleton |

---

## Development Settings

Control framework behavior via `DrnDevelopmentSettings`:

| Setting | Effect |
|---------|--------|
| `SkipValidation` | Skip service validation at startup |
| `TemporaryApplication` | Exit after service registration (for testing) |
| `Prototype` | Auto-recreate database for model changes |
| `AutoMigrate` | Apply pending migrations at startup |
| `LaunchExternalDependencies` | Start Docker containers |

---

## Common Patterns

### Adding a New Entity

```csharp
[EntityType(42)]  // Unique across application
public class Order : SourceKnownEntity
{
    public Guid CustomerId { get; private set; }
    public decimal Total { get; private set; }
    
    protected override EntityCreated? GetCreatedEvent() 
        => new OrderCreated(EntityId);
}
```

### Adding a New Service

```csharp
public interface IOrderService { ... }

[Scoped<IOrderService>]
public class OrderService : IOrderService
{
    public OrderService(
        IDbContext context,
        IScopedLog log,
        IScopedUser user) { }
}
```

### Adding a New Configuration Section

```csharp
[Config]
public class PaymentSettings
{
    [Required]
    public string ApiKey { get; set; } = null!;
    
    [Range(1, 30)]
    public int TimeoutSeconds { get; set; } = 10;
}
```

---

## Debugging Checklist

| Symptom | Check |
|---------|-------|
| Service not resolved | Verify `[Lifetime]` attribute, class is public/non-abstract |
| Config not binding | Check section name matches, validate appsettings path |
| ID generation fails | Verify `[EntityType]` attribute, check for duplicates |
| Migration error | Verify parameterless constructor on DbContext |
| Test context null | Ensure first parameter is `DrnTestContext` with correct attribute |
| Security headers missing | Verify `AppBuilderType.DrnDefaults` is set |

---

## Anti-Patterns to Avoid

| Anti-Pattern | Correct Approach |
|--------------|------------------|
| Manual service registration for DRN types | Use `[Scoped<T>]` attributes |
| Direct `new DbContext()` in application code | Inject via DI |
| `DateTime.Now` or `DateTime.UtcNow` | Use `IDateTimeProvider` or `DateTimeProvider.UtcNow` |
| Catching generic `Exception` | Use specific `DrnException` types |
| Hardcoding connection strings | Use `IAppSettings.GetRequiredConnectionString()` |
| Ignoring `ModifiedAt` on updates | Let `DrnContext` handle automatically |

---
