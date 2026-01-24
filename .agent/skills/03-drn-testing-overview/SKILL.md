---
name: drn-testing-overview
description: DRN.Testing philosophy (DTT), test organization, and testing approach across unit, integration, and performance tests
---

# DRN.Testing Overview

> Philosophy and organization of testing in DRN-Project using DTT (Duran's Testing Technique).

## When to Apply
- Understanding the testing philosophy and approach
- Choosing between unit, integration, or performance tests
- Setting up new test classes following DRN conventions
- Debugging test failures

---

## Testing Philosophy (DTT)

**DTT (Duran's Testing Technique)** is built on two core ideas:

1. **Writing tests should be easy and encouraging** - DrnTestContext handles wiring
2. **Tests should test actual usage** - Real containers, minimal mocking

```csharp
[Theory]
[DataInline]
public async Task MyTest(DrnTestContext context)
{
    context.ServiceCollection.AddApplicationServices();
    var service = context.GetRequiredService<IMyService>();
    // Test actual behavior
}
```

---

## Test Project Organization

```
DRN-Project/
├── DRN.Test.Unit/         # Fast, isolated unit tests
│   ├── Tests/             # Test classes organized by target
│   ├── Data/              # Test data files
│   └── Settings/          # Test configuration files
│
├── DRN.Test.Integration/  # Integration tests with containers
│   ├── Tests/             # Integration test classes
│   ├── Data/              # Test data
│   └── Settings/          # Configuration
│
└── DRN.Test.Performance/  # Benchmarks and load tests
    ├── Benchmark/         # BenchmarkDotNet tests
    ├── K6/                # K6 load test scripts
    └── Reports/           # Generated reports
```

---

## Test Context Types

| Context | Purpose | Use Case |
|---------|---------|----------|
| `DrnTestContext` | Full context with containers | Integration tests |
| `ContainerContext` | PostgreSQL testcontainer | Database tests |
| `WebApplicationContext` | WebApplicationFactory wrapper | API tests |
| `ApplicationContext` | Full app context | End-to-end tests |

---

## Test Pyramid

```text
          /\
         /  \
        / E2E\        (ApplicationContext)
       /______\       • Full app stack, auth, API, DB
      /        \
     / Integration\   (DrnTestContext)
    /______________\  • Real DB, Repositories, Use Cases (Preferred)
   /                \
  /    Unit Tests    \ (DrnTestContextUnit)
 /____________________\ • Domain Logic, Utils, isolated components
```

**DRN Philosophy (DTT)**:
While the traditional pyramid suggests a massive base of unit tests, **DTT favors Integration Tests** with real containers.
- **Unit Tests**: Use for pure logic, utilities, and complex domain invariants.
- **Integration Tests**: The "sweet spot" for reliability. Test real behavior with `Testcontainers`.
- **E2E Tests**: Critical user flows through the API.

---

## Data Attributes

| Attribute | Behavior |
|-----------|----------|
| `[DataInline]` | Auto-provides DrnTestContext + inlined values (Integration) |
| `[DataInlineUnit]` | Auto-provides DrnTestContextUnit (Unit) |
| `[DataMember(nameof(Method))]` | Member data + auto-generation |
| `[DataSelf]` | Class-based data source |
| `[FactDebuggerOnly]` | Runs only when debugger attached |
| `[TheoryDebuggerOnly]` | Theory that runs only with debugger |

```csharp
[Theory]
[DataInline(99)]
public void Test(DrnTestContext context, int value, IMockable mock)
{
    // value = 99 (inlined)
    // mock = NSubstitute mock (auto-generated)
    mock.Max.Returns(42);
}
```

---

## Test Categories

### Unit Tests (DRN.Test.Unit)
- **Purpose**: Fast, isolated logic testing
- **Dependencies**: Mocked via NSubstitute
- **Containers**: None
- **Speed**: Milliseconds

### Integration Tests (DRN.Test.Integration)
- **Purpose**: Test with real dependencies
- **Dependencies**: PostgreSQL testcontainers
- **Containers**: Started automatically
- **Speed**: Seconds

### Performance Tests (DRN.Test.Performance)
- **Purpose**: Benchmarks and load testing
- **Tools**: BenchmarkDotNet, K6
- **Output**: HTML/JSON reports

---

## Common Test Patterns

### Testing Services
```csharp
[Theory]
[DataInline]
public void ServiceTest(DrnTestContext context, IMockable dep)
{
    context.ServiceCollection.AddMyServices();
    dep.Method().Returns(expected);
    
    var sut = context.GetRequiredService<MyService>();
    sut.Execute().Should().Be(expected);
}
```

### Testing with Database
```csharp
[Theory]
[DataInline]
public async Task DbTest(DrnTestContext context)
{
    context.ServiceCollection.AddInfraServices();
    await context.ContainerContext.Postgres.ApplyMigrationsAsync();
    
    var dbContext = context.GetRequiredService<MyDbContext>();
    // Test with real database
}
```

### Testing API Endpoints
```csharp
[Theory]
[DataInline]
public async Task ApiTest(DrnTestContext context)
{
    var app = context.ApplicationContext.CreateApplication<Program>();
    await context.ContainerContext.Postgres.ApplyMigrationsAsync();
    
    var client = app.CreateClient();
    var response = await client.GetAsync("/api/resource");
    response.Should().BeSuccessful();
}
```

---

## Test Data Conventions

- **Settings files**: `Settings/` folder or same folder as test
- **Data files**: `Data/` folder or same folder as test
- Files must be marked as "Copy to Output Directory: Always"

```csharp
context.GetData("mydata.txt"); // Reads from Data/ or test folder
context.GetRequiredService<IAppSettings>(); // Uses Settings/appsettings.json
```

---

## Related Skills

| Skill | Focus |
|-------|-------|
| [08-testing.md](file:///Users/duranserkankilic/Work/Drn-Project/.agent/skills/08-testing.md) | DRN.Framework.Testing package details |
| [09-test-unit.md](file:///Users/duranserkankilic/Work/Drn-Project/.agent/skills/09-test-unit.md) | Unit test patterns |
| [10-test-integration.md](file:///Users/duranserkankilic/Work/Drn-Project/.agent/skills/10-test-integration.md) | Integration test patterns |
| [11-test-performance.md](file:///Users/duranserkankilic/Work/Drn-Project/.agent/skills/11-test-performance.md) | Performance testing |

---
