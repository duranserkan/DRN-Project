---
name: test-unit
description: Unit testing patterns and organization - Fast, isolated tests with auto-mocking (NSubstitute), service validation, test data management, and mocking strategies. Use for testing services, domain logic, and components in isolation. Keywords: unit-testing, mocking, nsubstitute, autofixture, test-patterns, service-testing, isolated-testing, dtt, xunit, skills, overview, drn, testing, test, integration
---

# DRN.Test.Unit

> Unit test patterns and organization for fast, isolated testing.

## When to Apply
- Writing new unit tests
- Understanding unit test organization
- Mocking dependencies effectively
- Testing services in isolation

---

## Project Structure

```
DRN.Test.Unit/
├── Tests/              # Test classes organized by target
│   └── Framework/      # Framework package tests
│       ├── Hosting/
│       ├── Utils/
│       └── EntityFramework/
├── Data/               # Test data files
├── Settings/           # Test configuration files
├── Sketch.cs           # Experimental/scratch tests
└── Usings.cs           # Global usings
```

---

## Test Patterns

### Basic Unit Test

```csharp
[Theory]
[DataInlineUnit]
public void Service_Should_DoExpectedBehavior(DrnTestContextUnit context, IDependency mock)
{
    // Arrange
    mock.GetValue().Returns(42);
    context.ServiceCollection.AddScoped<IDependency>(_ => mock);
    context.ServiceCollection.AddScoped<MyService>();
    
    // Act
    var service = context.GetRequiredService<MyService>();
    var result = service.Calculate();
    
    // Assert
    result.Should().Be(42);
}
```

### Testing with Inlined Data

```csharp
[Theory]
[DataInlineUnit(1, "first")]
[DataInlineUnit(2, "second")]
public void Test_With_Multiple_Cases(DrnTestContextUnit context, int id, string name)
{
    id.Should().BePositive();
    name.Should().NotBeEmpty();
}
```

### Testing Exceptions

```csharp
[Theory]
[DataInlineUnit]
public void Service_Should_Throw_OnInvalidInput(DrnTestContextUnit context)
{
    context.ServiceCollection.AddScoped<MyService>();
    var service = context.GetRequiredService<MyService>();
    
    var act = () => service.Process(null!);
    
    act.Should().Throw<ValidationException>()
       .WithMessage("*input*");
}
```

---

## Mocking Strategies

### Auto-Mocking (Preferred)

```csharp
[Theory]
[DataInlineUnit]
public void Test(DrnTestContextUnit context, IRepository mock)
{
    // mock is auto-created by NSubstitute
    mock.FindById(1).Returns(new Entity());
    
    context.ServiceCollection.AddScoped<IRepository>(_ => mock);
    context.ServiceCollection.AddScoped<MyService>();
    
    var service = context.GetRequiredService<MyService>();
    // Test with mocked repository
}
```

### Multiple Mocks

```csharp
[Theory]
[DataInlineUnit]
public void Test(DrnTestContextUnit context, IRepo repo, ILogger logger, ICache cache)
{
    // All three are auto-mocked
    repo.Get().Returns(data);
    
    // Register all mocks
    context.ServiceCollection.AddScoped<IRepo>(_ => repo);
    context.ServiceCollection.AddScoped<ILogger>(_ => logger);
    context.ServiceCollection.AddScoped<ICache>(_ => cache);
}
```

### Verifying Calls

```csharp
[Theory]
[DataInlineUnit]
public void Test(DrnTestContextUnit context, IEventPublisher publisher)
{
    // ... setup and act ...
    
    publisher.Received(1).Publish(Arg.Any<DomainEvent>());
    publisher.DidNotReceive().Publish(Arg.Is<DomainEvent>(e => e.Type == "Error"));
}
```

---

## Service Validation

```csharp
[Theory]
[DataInlineUnit]
public async Task Validate_All_Dependencies(DrnTestContextUnit context)
{
    context.ServiceCollection.AddSampleApplicationServices();
    context.ServiceCollection.AddSampleInfraServices();
    
    await context.ValidateServicesAsync(); // Validates all attribute-based services
}
```

---

## Test Data

### Settings Folder

```
DRN.Test.Unit/
└── Settings/
    ├── appsettings.json       # Default test settings
    └── mytest-settings.json   # Test-specific settings
```

### Data Folder

```
DRN.Test.Unit/
└── Data/
    ├── test-input.json
    └── expected-output.json
```

### Accessing Data

```csharp
[Theory]
[DataInlineUnit("user-data.json")]
public void Test_With_Data(DrnTestContextUnit context, string dataFile)
{
    var content = context.GetData(dataFile);
    var users = JsonSerializer.Deserialize<List<User>>(content);
}
```

---

## Sketch.cs Pattern

Use `Sketch.cs` for experimental or debugging tests:

```csharp
public class Sketch
{
    [Theory]
    [DataInlineUnit]
    public void Experiment(DrnTestContextUnit context)
    {
        // Quick experiments here
        // Not meant to be permanent tests
    }
}
```

---

## Global Usings (Usings.cs)

```csharp
global using Xunit;
global using AutoFixture;
global using AutoFixture.AutoNSubstitute;
global using DRN.Framework.Utils.Extensions;
global using DRN.Framework.SharedKernel;
global using DRN.Framework.Utils.Settings;
global using DRN.Framework.Utils.DependencyInjection;
global using DRN.Framework.Testing;
global using DRN.Framework.Testing.DataAttributes;
global using DRN.Framework.Testing.Providers;
global using DRN.Framework.Testing.TestAttributes;
global using DRN.Framework.Testing.Contexts;
global using AwesomeAssertions;
global using Microsoft.Extensions.DependencyInjection;
global using NSubstitute;
```

---

## Related Skills

- [overview-drn-testing.md](../overview-drn-testing/SKILL.md) - Testing philosophy
- [drn-testing.md](../drn-testing/SKILL.md) - Framework.Testing package
- [test-integration.md](../test-integration/SKILL.md) - Integration testing

---
