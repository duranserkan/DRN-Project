# Lessons Learned

## 1. DTT Data Attributes — Parameterized Testing with `DataInline` / `DataInlineUnit`

### How DTT Data Attributes Work

All DTT data attributes (`DataInline`, `DataMember`, `DataSelf` and their `Unit` variants) share identical parameter-resolution behavior:

| Step | What Happens |
|------|-------------|
| 1. Context | `DrnTestContext` or `DrnTestContextUnit` auto-provided as first parameter when the method signature requests it |
| 2. Inline values | Attribute arguments mapped to subsequent method parameters in order |
| 3. AutoFixture | Remaining parameters without inline values auto-generated (primitives, `Guid`, POCOs, etc.) |
| 4. NSubstitute | Interface/abstract-class parameters auto-mocked; mocks auto-replace matching registrations in `ServiceCollection` |

### Attribute Variants

| Integration Test | Unit Test | Context Provided |
|-----------------|-----------|-----------------|
| `[DataInline]` | `[DataInlineUnit]` | `DrnTestContext` / `DrnTestContextUnit` |
| `[DataMember]` | `[DataMemberUnit]` | Same as above |
| `[DataSelf]` | `[DataSelfUnit]` | Same as above |

`DrnTestContextUnit` is lightweight (no `ContainerContext`, `ApplicationContext`, or `FlurlHttpTest`).

### Consolidation Pattern

**When**: Logic has discrete input→output permutations and the test body is identical across cases — consolidate into one `[Theory]` with multiple `[DataInline(...)]` / `[DataInlineUnit(...)]` rows.

**Anti-pattern** (5 methods, ~65 lines):

```csharp
[Theory]
[DataInlineUnit]
public void Migrate_Should_Be_True_In_Dev_When_Enabled(DrnTestContextUnit context) { /* identical body with different config */ }

[Theory]
[DataInlineUnit]
public void Migrate_Should_Be_False_In_Dev_When_Disabled(DrnTestContextUnit context) { /* identical body with different config */ }
// ... 3 more methods
```

**Preferred** (1 method, ~17 lines):

```csharp
[Theory]
[DataInlineUnit(AppEnvironment.Development, true, false, true)]   // Dev + AutoMigrateDev=on  → migrate
[DataInlineUnit(AppEnvironment.Development, false, true, false)]  // Dev + AutoMigrateDev=off → no migrate
[DataInlineUnit(AppEnvironment.Staging, true, false, false)]      // Staging ignores Dev flag
[DataInlineUnit(AppEnvironment.Staging, false, true, true)]       // Staging + AutoMigrateStaging=on → migrate
[DataInlineUnit(AppEnvironment.Production, true, true, false)]    // Production → never migrate
public void Migrate_Flag_Should_Reflect_Environment_And_AutoMigrate_Settings(DrnTestContextUnit context,
    AppEnvironment environment, bool autoMigrateDevelopment, bool autoMigrateStaging, bool migrationEnabled)
{
    ConfigureEnvironment(context, environment, autoMigrateDevelopment, autoMigrateStaging);
    var status = context.GetRequiredService<DevelopmentStatus>();
    var model = CreateChangeModel();
    status.AddChangeModel(model);
    model.Flags.Migrate.Should().Be(migrationEnabled);
}
```

### Rules

1. **Last parameter = expected result** — each `[DataInlineUnit]` row is a self-contained specification
2. **Name covers the dimension** — e.g., `..._Should_Reflect_Environment_And_AutoMigrate_Settings`, not a name tied to one specific case
3. **Comment inline data** — trailing comment on each attribute row when values aren't self-explanatory
4. **Extract shared setup** — private helper keeps the test body focused on act + assert
5. **Omit inline values for auto-generated params** — let AutoFixture/NSubstitute handle params you don't need to control
6. **Don't consolidate when** — test bodies differ structurally, require different setup/teardown, or separate failure messages aid debugging more than parameterization
