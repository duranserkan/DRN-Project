---
name: test-unit
description: Unit testing patterns and organization - Fast, isolated tests with auto-mocking (NSubstitute), service validation, test data management, and mocking strategies. Use for testing services, domain logic, and components in isolation. Keywords: unit-testing, mocking, nsubstitute, autofixture, test-patterns, service-testing, isolated-testing, dtt, xunit
last-updated: 2026-06-06
difficulty: basic
tokens: ~1K
---

# DRN.Test.Unit

> Unit test patterns for fast, isolated testing.

## When to Apply
- Writing new unit tests
- Mocking dependencies effectively
- Testing services in isolation

---

## Test Patterns

### Attribute Selection

- Use `[Fact]` when the test has no inline data, generated parameters, or `DrnTestContextUnit` dependency.
- Use `[Theory]` + `[DataInlineUnit]` for parameterized rows or auto-generated parameters.
- Include `DrnTestContextUnit` in the method signature only when the test uses context services, configuration, data files, or service validation.

```csharp
[Fact]
public void Trim_Should_Remove_Outer_Whitespace()
{
    "  Duran  ".Trim().Should().Be("Duran");
}

[Theory]
[DataInlineUnit(2, 3, 5)]
[DataInlineUnit(-1, -2, -3)]
public void Add_Should_Return_Correct_Sum(int a, int b, int expected)
{
    (a + b).Should().Be(expected);
}

[Theory]
[DataInlineUnit("SafeSection", "Visible", "safe-value")]
public void Configuration_Should_Be_Available_When_Context_Is_Requested(
    DrnTestContextUnit context, string section, string key, string value)
{
    context.AddToConfiguration(section, key, value);
    var debugView = context.GetConfigurationDebugView();

    debugView.SettingsByProvider.Values.SelectMany(settings => settings)
        .Should().Contain($"{section}:{key}={value}");
}
```

### Unit Test with Auto-Mocking

```csharp
[Theory]
[DataInlineUnit]
public void Service_Should_DoExpectedBehavior(DrnTestContextUnit context, IDependency mock)
{
    // Arrange: mock is auto-created by NSubstitute
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

### Test Consolidation

If tests share the same setup and their consolidation creates no semantic or performance issue, they should be unified. Apply when consolidation requires only minimal essential change.

#### Parameterized

When multiple cases share identical test bodies, consolidate into one `[Theory]` with multiple `[DataInlineUnit]` rows:

```csharp
[Theory]
[DataInlineUnit(2, 3, 5)]     // positive + positive
[DataInlineUnit(-1, -2, -3)]  // negative + negative
[DataInlineUnit(0, 0, 0)]     // zeros
public void Add_Should_Return_Correct_Sum(int a, int b, int expected)
{
    (a + b).Should().Be(expected);
}
```

**Rules**: Last param = expected result · Name covers the dimension, not one case · Comment rows when values aren't obvious · Don't consolidate when test bodies differ structurally.

#### Flow

When tests share identical setup and additional assertions can be applied by continuing the existing test flow, unify into a single test to prevent code duplication and maintenance burden. Less common in unit tests (setup is cheap) but applies when multiple verifications share the same mock/service wiring.

### Exception Testing

```csharp
[Theory]
[DataInlineUnit]
public void Should_Throw_OnInvalid(DrnTestContextUnit context)
{
    context.ServiceCollection.AddScoped<MyService>();
    var service = context.GetRequiredService<MyService>();
    
    var act = () => service.Process(null!);
    act.Should().Throw<ValidationException>().WithMessage("*input*");
}
```

### Verifying Mock Calls

```csharp
publisher.Received(1).Publish(Arg.Any<DomainEvent>());
publisher.DidNotReceive().Publish(Arg.Is<DomainEvent>(e => e.Type == "Error"));
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

| Folder | Purpose | Access |
|--------|---------|--------|
| `Settings/` | Test configuration (appsettings.json) | Auto-loaded by DrnTestContextUnit |
| `Data/` | Test data files | `context.GetData("file.json")` |

### Sketch.cs

Use for experimental or debugging tests — not permanent, not CI-gated.

---

## Related Skills

- [drn-testing.md](../drn-testing/SKILL.md) - Framework.Testing package
- [overview-drn-testing.md](../overview-drn-testing/SKILL.md) - Testing philosophy
- [test-integration.md](../test-integration/SKILL.md) - Integration testing

---

## Global Usings

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
