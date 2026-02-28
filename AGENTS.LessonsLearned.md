# Lessons Learned

## DTT Data Attributes

### Parameter Resolution

All DTT data attributes (`DataInline`, `DataMember`, `DataSelf` and `Unit` variants) resolve parameters identically:

| Step | What Happens |
|------|-------------|
| 1. Context | `DrnTestContext` or `DrnTestContextUnit` auto-provided as first parameter when the method signature requests it |
| 2. Inline values | Attribute arguments mapped to subsequent method parameters in order |
| 3. AutoFixture | Remaining parameters without inline values auto-generated (primitives, `Guid`, POCOs, etc.) |
| 4. NSubstitute | Interface/abstract-class parameters auto-mocked; mocks auto-replace matching registrations in `ServiceCollection` |

### Attribute Variants

| Integration Test | Unit Test |
|-----------------|----------|
| `[DataInline]` | `[DataInlineUnit]` |
| `[DataMember]` | `[DataMemberUnit]` |
| `[DataSelf]` | `[DataSelfUnit]` |

Integration variants provide `DrnTestContext`; Unit variants provide `DrnTestContextUnit` (lightweight — no `ContainerContext`, `ApplicationContext`, or `FlurlHttpTest`).

### Test Consolidation

If tests share the same setup and their consolidation creates no semantic or performance issue, they should be unified. Apply when consolidation requires only minimal essential change.

#### Parameterized

**When**: Logic has discrete input→output permutations and test body is identical across cases — consolidate into one `[Theory]` with multiple `[DataInline(...)]` / `[DataInlineUnit(...)]` rows.

**Anti-pattern**: Separate `[Theory]` methods per input permutation with identical bodies (e.g., 5 methods, ~65 lines).

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

#### Flow

**When**: Tests share identical setup (container init, migrations, service registration) and assertions can continue in the same flow — unify into a single test.

**Reference**: `QAContextTagTests.cs` — single flow validating entity IDs, JSON queries, date filters, and materialization interceptor.

#### Rules

1. **Last parameter = expected result** — each `[DataInlineUnit]` row is a self-contained specification
2. **Name covers the dimension** — e.g., `..._Should_Reflect_Environment_And_AutoMigrate_Settings`, not a name tied to one specific case
3. **Comment inline data** — trailing comment on each attribute row when values aren't self-explanatory
4. **Extract shared setup** — private helper keeps the test body focused on act + assert
5. **Omit inline values for auto-generated params** — let AutoFixture/NSubstitute handle params you don't need to control
6. **Don't consolidate when** — test bodies differ structurally, require different setup/teardown, or separate failure messages aid debugging more than parameterization
