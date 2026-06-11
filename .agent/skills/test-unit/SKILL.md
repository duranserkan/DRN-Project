---
name: test-unit
description: Use when adding or reviewing fast isolated tests for pure logic, deterministic branches, service wiring, validation, or regression coverage without external infrastructure.
last-updated: 2026-06-12
difficulty: basic
tokens: ~0.7K
---

# Unit Testing

> Portable unit-test guidance. Load the repository profile first; if it declares custom test attributes or contexts, apply the relevant framework/profile testing skill before using this generic guide.

## Attribute Choice

- Use the framework's simple test attribute for tests without inline data.
- Use parameterized tests for repeated behavior with multiple rows.
- Use member/class data when row setup is too large for inline attributes.
- Request fixture/context parameters only when the test needs DI, configuration, data files, or service validation.

```csharp
[Theory]
[InlineData(2, 3, 5)]
[InlineData(-1, -2, -3)]
public void Add_Should_Return_Correct_Sum(int a, int b, int expected)
{
    (a + b).Should().Be(expected);
}
```

## Unit Scope

Good unit targets:

- pure domain invariants and value logic
- utilities and deterministic branching
- service behavior where collaborators can be mocked honestly
- validation, mapping, formatting, and parsing
- DI/service registration checks that do not need external infrastructure

Prefer integration tests when persistence, SQL, interceptors, middleware, auth, serialization boundaries, or real transport behavior matter.

## Consolidation

Parameterize identical bodies with multiple rows. Extend an existing test class when it already owns the component. Keep separate tests when setup, behavior, or assertion shape differs enough that a combined flow becomes harder to read.

## Service Validation

Use the repository's DI container and service validation helpers if they exist. Keep service-validation tests narrow: prove the graph composes and important scoped/singleton rules hold; do not turn them into broad behavior tests.

## Verification

Run unit tests only when the user and repository instructions allow test execution. Use the command from the repository profile; if absent, discover the narrowest unit-test project or package script.

## Related

- [test-integration](../test-integration/SKILL.md)
- [basic-code-review](../basic-code-review/SKILL.md)
