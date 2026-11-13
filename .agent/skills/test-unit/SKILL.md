---
name: test-unit
description: Use when adding or reviewing fast isolated tests for pure logic, deterministic branches, service wiring, validation, or regression coverage without external infrastructure.
last-updated: 2026-11-13
difficulty: basic
---

# Unit Testing

Load the repository profile first. Apply its testing skill for custom attributes and contexts before this generic guide.

## Attribute Choice

- Use the framework's simple test attribute for tests without inline data.
- Use parameterized tests for repeated behavior with multiple rows.
- Use member/class data when row setup is too large for inline attributes.
- Request fixture/context parameters only when the test needs DI, configuration, data files, or service validation.

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

## Composed Decisions

Use modified condition/decision coverage (MC/DC) when each condition must independently affect a decision. Pair fixtures that change only the target condition and flip the result. For a simple AND, hold the other conditions true. For a simple OR, hold them false. For mixed expressions, verify each pair against the full expression. Keep a positive full-expression fixture and assert exact outputs or membership. Add broader combinations only for distinct interactions. Apply this to authorization, validation, feature gates, and query filters.

## Service Validation

Use the repository's DI container and validation helpers when available. Prove the graph composes and scoped/singleton rules hold. Keep behavior tests separate.

## Verification

Follow the [diagnostic suppression guidance](../basic-code-review/SKILL.md#diagnostic-suppressions).

Run unit tests only when user and repository instructions allow execution. Use the profile command or discover the narrowest unit-test project or package script.

## Related

- [test-integration](../test-integration/SKILL.md)
- [basic-code-review](../basic-code-review/SKILL.md)
