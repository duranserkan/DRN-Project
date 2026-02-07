Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.7.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals.

> [!WARNING]
> Since v0.6.0 (released 10 November 2024), substantial changes have occurred. This release notes file has been reset to reflect the current state of the project as of 29 January 2026. Previous history has been archived to maintain a clean source of truth based on the current codebase.

### New Features

*   **DrnTestContext & DTT**
    *   **Full Integration Context**: `DrnTestContext` provides `ServiceCollection`, `ServiceProvider`, `Configuration`, and `FlurlHttpTest`.
    *   **Auto-Registration**: Automatically adds `DrnUtils` and executes `[StartupJob]`s for one-time setups.
    *   **Method Context**: Captures metadata for folder-based settings resolution.
    *   **DI Validation**: `ValidateServicesAsync()` for verifying service collection health and identifying missing dependencies early.
    *   **Lightweight Unit Context**: `DrnTestContextUnit` for pure unit tests without container overhead.
*   **Container Orchestration**
    *   **ContainerContext**: Integrated management of PostgreSQL and RabbitMQ Testcontainers.
    *   **Auto-Wiring**: Scans for `DrnContext`s, creates containers, applies migrations, and injects connection strings automatically.
    *   **Modes**: Supports shared containers (fast) or `.Isolated` containers (independent data).
    *   **Rapid Prototyping**: `EnsureDatabaseAsync` for schema generation without migrations.
*   **Application Integration**
    *   **ApplicationContext**: Deep integration with `WebApplicationFactory`.
    *   **Helpers**: `CreateClientAsync` (starts app + migrations + auth client), `CreateApplicationAndBindDependenciesAsync`, `LogToTestOutput`.
*   **Local Development Experience**
    *   **Infrastructure Management**: `LaunchExternalDependenciesAsync` for `WebApplicationBuilder` to automatically start containers (Postgres, RabbitMQ) during development, ensuring zero-configuration onboarding for new developers.
*   **Data Attributes (Auto-Mocking)**
    *   **DataInline**: Replaces `[InlineData]`. Auto-mocks interfaces (NSubstitute), fills missing params (AutoFixture), provides `DrnTestContext`.
    *   **DataMember**: Replaces `[MemberData]`. Source data from properties with auto-mocking support.
    *   **DataSelf**: Self-contained test data classes inheriting `DataSelfAttribute` (using `AddRow`).
    *   **Debugger Attributes**: `[FactDebuggerOnly]` and `[TheoryDebuggerOnly]` for running tests only during debugging sessions.
*   **Providers & Utilities**
    *   **SettingsProvider**: Loads `appsettings.json` and overrides from `Settings/` folder or test-local folder.
    *   **DataProvider**: Loads test data files (e.g., `.json`, `.txt`) from `Data/` folder or test-local folder.
    *   **CredentialsProvider**: Generates unique, consistent usernames/passwords for test authentication.
    *   **JSON Utilities**: `ValidateObjectSerialization<T>()` for one-line JSON round-trip contract verification.

---

Documented with the assistance of [DiSC OS](https://github.com/duranserkan/DRN-Project/blob/develop/DiSCOS/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**