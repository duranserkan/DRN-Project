[![master](https://github.com/duranserkan/DRN-Project/actions/workflows/master.yml/badge.svg?branch=master)](https://github.com/duranserkan/DRN-Project/actions/workflows/master.yml)
[![develop](https://github.com/duranserkan/DRN-Project/actions/workflows/develop.yml/badge.svg?branch=develop)](https://github.com/duranserkan/DRN-Project/actions/workflows/develop.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)


[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=bugs)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=ncloc)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=coverage)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)

# DRN.Framework.Testing

> Practical, effective testing helpers with data attributes, test context, and container orchestration for unit and integration tests.

## TL;DR

- **Auto-Mocking** - `[DataInline]` provides `DrnTestContext` and auto-mocks interface parameters with NSubstitute
- **Container Context** - One-line Postgres/RabbitMQ container setup with auto-migration
- **Application Context** - Full `WebApplicationFactory` integration with container awareness
- **Convention-Based** - Settings and data files auto-discovered from test folder hierarchy
- **DTT Pattern** - Duran's Testing Technique for clean, hassle-free testing

## Table of Contents

- [QuickStart: Beginner](#quickstart-beginner)
- [QuickStart: Advanced](#quickstart-advanced)
- [DrnTestContext](#drntestcontext)
- [ContainerContext](#containercontext)
- [ApplicationContext](#applicationcontext)
- [Local Development Experience](#local-development-experience)
- [Data Attributes](#data-attributes)
- [Unit Testing](#unit-testing)
- [DebugOnly Tests](#debugonly-tests)
- [DI Health Validation](#di-health-validation)
- [JSON Utilities](#json-utilities)
- [FlurlHttpTest Integration](#flurlhttptest-integration)
- [Providers](#providers)
- [Example Test Project](#example-test-project-csproj-file)
- [Test Snippet](#test-snippet)
- [Testing Guide and DTT Approach](#testing-guide-and-dtt-approach)
- [Global Usings](#global-usings)
- [Related Packages](#related-packages)

---

## QuickStart: Beginner

Write your first auto-mocked test in seconds:
```csharp
    [Theory]
    [DataInline]
    public void DataInlineDemonstration(DrnTestContext context, IMockable autoInlinedDependency)
    {
        context.ServiceCollection.AddApplicationServices();
        //Context wraps service provider and automagically replaces actual dependencies with auto inlined dependencies
        var dependentService = context.GetRequiredService<DependentService>();
        
        autoInlinedDependency.Max.Returns(int.MaxValue); //dependency is already mocked by NSubstitute
        dependentService.Max.Should().Be(int.MaxValue); //That is all. It is clean and effective 
    }
```

### Testing models used in the QuickStart
```csharp

public static class ApplicationModule //Can be defined in Application Layer or in Hosted App
{
    public static void AddApplicationServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<IMockable, ToBeRemovedService>(); //will be removed by test context because test method requested mocked interface
        serviceCollection.AddTransient<DependentService>(); //dependent service uses IMockable and Max property returns dependency's Max value
    }
}

public interface IMockable
{
    public int Max { get; }
}

public class ToBeRemovedService : IMockable
{
    public int Max { get; set; }
}

public class DependentService : IMockable
{
    private readonly IMockable _mockable;

    public DependentService(IMockable mockable)
    {
        _mockable = mockable;
    }

    public int Max => _mockable.Max;
}
```

## QuickStart: Advanced

Advanced example with inlined values, auto-generated data, and mocked interfaces:

- `DataInline` provides `DrnTestContext` as first parameter
- Then it provides inlined values
- Then it auto-generates missing values with AutoFixture
- `AutoFixture` mocks any interface parameter with `NSubstitute`
```csharp
/// <param name="context"> Provided by DataInline even if it is not a compile time constant</param>
/// <param name="inlineData">Provided by DataInline</param>
/// <param name="autoInlinedData">DataInline will provide missing data with the help of AutoFixture</param>
/// <param name="autoInlinedMockable">DataInline will provide implementation mocked by NSubstitute</param>
[Theory]
[DataInline(99)]
public void TextContext_Should_Be_Created_From_DrnTestContextData(DrnTestContext context, int inlineData, Guid autoInlinedData, IMockable autoInlinedMockable)
{
    inlineData.Should().Be(99);
    autoInlinedData.Should().NotBeEmpty(); //guid generated by AutoFixture
    autoInlinedMockable.Max.Returns(int.MaxValue); //dependency mocked by NSubstitute

    context.ServiceCollection.AddApplicationServices(); //you can add services, modules defined in hosted app, application, infrastructure layer etc..
    var serviceProvider = context.BuildServiceProvider(); //appsettings.json added by convention. Context and service provider will be disposed by xunit
    serviceProvider.GetService<ToBeRemovedService>().Should().BeNull(); //Service provider behaviour demonstration

    var dependentService = serviceProvider.GetRequiredService<DependentService>();
    dependentService.Max.Should().Be(int.MaxValue);
}
```

## DrnTestContext
`DrnTestContext` has following properties:
* captures values provided to running test method, test method info and location.
* provides `ServiceCollection` so that to be tested services and dependencies can be added before building `ServiceProvider`.
* provides and implements lightweight `ServiceProvider` that contains default logging without any provider
  * `ServiceProvider` can provide services that depends on like `ILogger<DefaultService>`
  * logged data will not be leaked to anywhere since it has no logging provider.
* provides `ContainerContext`
  * can start `postgres` and `rabbitmq` containers, apply migrations for dbContexts derived from DrnContext and updates connection string configuration with a single line of code
* provides `ApplicationContext`
  * syncs `DrnTestContext` service collection and service provider with provided application by WebApplicationFactory
  * supports `ITestOutputHelper` integration for capturing application logs in test output
* provides `FlurlHttpTest` for mocking external HTTP requests (see [FlurlHttpTest Integration](#flurlhttptest-integration))
* provides `IConfiguration` and `IAppSettings` with SettingsProvider by using convention.
  * settings.json file can be found in the same folder with test
  * settings.json file can be found in the global Settings folder or Settings folder that stays in the test folder
  * Make sure file is copied to output directory
  * If no settings file is specified while calling `BuildServiceProvider`. `appsettings.json` file be searched by convention.
* provides data file contents by using convention.
  * data file can be found in the same folder with test
  * data file can be found in the global Data folder or Data folder that stays in the test folder
  * Make sure file is copied to output directory
* triggers `StartupJobRunner` to execute one-time test setup jobs marked with `ITestStartupJob`
* `ServiceProvider` provides utils provided with DRN.Framework.Utils' `UtilsModule`
* `BuildServiceProvider` replaces dependencies that can be replaced with inlined interfaces.
* `ServiceProvider` and `DrnTestContext` will be disposed by xunit when test finishes
* **DI Health Check**: `ValidateServicesAsync()` ensures that all services added to `ServiceCollection` (including those via attributes) can be resolved without runtime errors.

`settings.json` can be put in the same folder that test file belongs. This way providing and isolating test settings is much easier
```csharp
    [Theory]
    [DataInline( "localhost")]
    public void DrnTestContext_Should_Add_Settings_Json_To_Configuration(DrnTestContext context, string value)
    {
        //settings.json file can be found in the same folder with test file, in the global Settings folder or Settings folder that stays in the same folder with test file
        context.GetRequiredService<IAppSettings>().GetRequiredSection("AllowedHosts").Value.Should().Be(value);
    }
```
`data.txt` can be put in the same folder that test file belongs. This way providing and isolating test data is much easier
```csharp
    [Theory]
    [DataInline("data.txt", "Atatürk")]
    [DataInline("alternateData.txt", "Father of Turks")]
    public void DrnTestContext_Should_Return_Test_Specific_Data(DrnTestContext context, string dataPath, string data)
    {
        //data file can be found in the same folder with test file, in the global Data folder or Data folder that stays in the same folder with test file
        context.GetData(dataPath).Should().Be(data);
    }
```

## ContainerContext
With `ContainerContext` and conventions you can easily write effective integration tests against your database and message queue dependencies.

### PostgreSQL Container
```csharp
    [Theory]
    [DataInline]
    public async Task QAContext_Should_Add_Category(DrnTestContext context)
    {
        context.ServiceCollection.AddSampleInfraServices();
        await context.ContainerContext.Postgres.ApplyMigrationsAsync();
        var qaContext = context.GetRequiredService<QAContext>();

        var category = new Category("dotnet8");
        qaContext.Categories.Add(category);
        await qaContext.SaveChangesAsync();
        category.Id.Should().BePositive();
    }
```
* Application modules can be registered without any modification to `DrnTestContext`
* `DrnTestContext`'s `ContainerContext`
  * creates `postgresql` and `rabbitmq` containers then scans DrnTestContext's service collection for inherited DrnContexts.
  * Adds connection strings to DrnTestContext's configuration for each derived `DrnContext` according to convention.
* `DrnTestContext` acts as a ServiceProvider and when a service is requested it can build it from service collection with all dependencies.

### RabbitMQ Container

You can start a RabbitMQ container for testing message queue integrations:

```csharp
[Theory]
[DataInline]
public async Task RabbitMQ_Integration_Test(DrnTestContext context)
{
    var container = await context.ContainerContext.RabbitMq.StartAsync();
    var connectionString = container.GetConnectionString();
    
    // Use connectionString for your message queue tests
}
```

### Advanced Container Configuration

You can customize the Postgres container before starting it using `PostgresContainerSettings`:

```csharp
[Theory]
[DataInline]
public async Task Custom_Container_Verification(DrnTestContext context)
{
    // Configure settings before accessing ContainerContext.Postgres
    PostgresContext.PostgresContainerSettings = new PostgresContainerSettings
    {
        ContainerName = "my-custom-db",
        Database = "custom_db",
        HostPort = 5440 // Bind to specific host port
    };
    
    await context.ContainerContext.StartPostgresAndApplyMigrationsAsync();
    // ...
}
```

### Isolated Containers

By default, `DrnTestContext` shares a single Postgres container across tests for performance. For scenarios requiring complete isolation (e.g., changing global system state), use `PostgresContextIsolated`:

```csharp
[Theory]
[DataInline]
public async Task Isolated_Test_Run(DrnTestContext context)
{
    // Starts a FRESH, exclusive container for this test
    var container = await context.ContainerContext.Postgres.Isolated.ApplyMigrationsAsync();
    
    // ... use the isolated container ...
}
```

### Rapid Prototyping (No Migrations)

For rapid development where migrations are not yet created, use `EnsureDatabaseAsync` to create the schema directly from the model:

```csharp
    await context.ContainerContext.Postgres.Isolated.EnsureDatabaseAsync<MyDbContext>();
```


## ApplicationContext
`ApplicationContext` syncs `DrnTestContext` service collection and service provider with provided application by WebApplicationFactory.
* You can provide or override configurations and services to your program until you force `WebApplicationFactory` to build a `Host` such as creating `HttpClient` or requesting `TestServer`.
* Supports `ITestOutputHelper` integration to capture application logs in test output
* **Automatic Flag Management**: automatically sets the following flags to ensure test isolation and prevent collision with local development infrastructure:
  *   `TestEnvironment.DrnTestContextEnabled` = `true`
  *   `AppSettings.DevelopmentSettings.TemporaryApplication` = `true`

### Basic Usage
```csharp
    [Theory]
    [DataInline]
    public async Task ApplicationContext_Should_Provide_Configuration_To_Program(DrnTestContext context)
    {
        var webApplication = context.ApplicationContext.CreateApplication<Program>();
        await context.ContainerContext.Postgres.ApplyMigrationsAsync();
        
        var client = webApplication.CreateClient();
        var forecasts = await client.GetFromJsonAsync<WeatherForecast[]>("WeatherForecast");
        forecasts.Should().NotBeNull();

        var appSettingsFromWebApplication = webApplication.Services.GetRequiredService<IAppSettings>();
        var connectionString = appSettingsFromWebApplication.GetRequiredConnectionString(nameof(QAContext));
        connectionString.Should().NotBeNull();

        var appSettingsFromDrnTestContext = context.GetRequiredService<IAppSettings>();
        appSettingsFromWebApplication.Should().BeSameAs(appSettingsFromDrnTestContext);//resolved from same service provider
    }
```

### Simplified Client Creation

For most API testing scenarios, use `CreateClientAsync` which handles common setup:

```csharp
    [Theory]
    [DataInline]
    public async Task Simplified_API_Test(DrnTestContext context, ITestOutputHelper output)
    {
        // Automatically starts containers, applies migrations, and returns authenticated client
        var client = await context.ApplicationContext.CreateClientAsync<Program>(output);
        
        var response = await client.GetAsync("/api/endpoint");
        response.Should().BeSuccessful();
    }
```

### Test Output Logging

Capture application logs in test output for debugging:

```csharp
    [Theory]
    [DataInline]
    public async Task Test_With_Logging(DrnTestContext context, ITestOutputHelper output)
    {
        context.ApplicationContext.LogToTestOutput(output);
        var app = context.ApplicationContext.CreateApplication<Program>();
        
        // Application logs will appear in test output
    }
```

## Local Development Experience

`DRN.Framework.Testing` enhances local development by providing infrastructure management capabilities directly to the host application.

### Setup

To use this feature in your main application (not in test projects), you must add a reference to `DRN.Framework.Testing` that is **only active in Debug configuration**. This prevents test dependencies from leaking into production builds.

```xml
<ItemGroup Condition="'$(Configuration)' == 'Debug'">
    <ProjectReference Include="..\DRN.Framework.Testing\DRN.Framework.Testing.csproj" />
</ItemGroup>
```

### LaunchExternalDependenciesAsync

This extension method on `WebApplicationBuilder` automatically launches external dependencies (like Postgres, RabbitMQ) using Testcontainers when the application starts in a development environment.

```csharp
// In your DrnProgramActions implementation (e.g., SampleProgramActions.cs)
#if DEBUG
public override async Task ApplicationBuilderCreatedAsync<TProgram>(
    TProgram program, WebApplicationBuilder builder,
    IAppSettings appSettings, IScopedLog scopedLog)
{
    var launchOptions = new ExternalDependencyLaunchOptions
    {
        PostgresContainerSettings = new PostgresContainerSettings
        {
            Reuse = true, // Keep container running across restarts
            HostPort = 6432 // Bind to a specific port to avoid conflicts
        }
    };
    
    // Automatically starts containers if they are not already running
    await builder.LaunchExternalDependenciesAsync(scopedLog, appSettings, launchOptions);
}
#endif
```

### Launch Conditions

`LaunchExternalDependenciesAsync` is designed to be safe and non-intrusive. It only executes when all following conditions are met:
1. **Environment**: Must be `Development`.
2. **Launch Flag**: `AppSettings.DevelopmentSettings.LaunchExternalDependencies` must be `true`.
3. **Not in Test**: `TestEnvironment.DrnTestContextEnabled` must be `false` (prevents collision with test containers).
4. **Not Temporary**: `AppSettings.DevelopmentSettings.TemporaryApplication` must be `false`.

This feature is particularly useful for:
*   **Onboarding**: New developers can run the app without manually setting up infrastructure.
*   **Consistency**: Ensures all developers use the same infrastructure configuration.
*   **Rapid Prototyping**: Quickly spin up throwaway databases.

---

## Connection String Resolution

The framework uses different strategies for connection string resolution. See the comprehensive flow diagram in [DRN.Framework.EntityFramework README](../DRN.Framework.EntityFramework/README.md#connection-string-resolution-by-environment).

### Key Scenarios

| Scenario | Connection Source | Settings Used |
|----------|-------------------|---------------|
| **Production/Staging** | `ConnectionStrings:{ContextName}` | Explicit config only |
| **Local Debug** | `LaunchExternalDependenciesAsync()` | `PostgresContainerSettings.DefaultPassword` |
| **Docker/K8s Dev** | `DrnContextDevelopmentConnection` | `DbContextConventions->DrnContext_Dev* & postgres-password` |
| **DrnTestContext** | `ContainerContext.Postgres` | `PostgresContainerSettings` defaults |

> [!IMPORTANT]
> **Testcontainers Mode** (tests or `LaunchExternalDependencies = true`):
> - `postgres-password` and `DrnContext_Dev*` settings are **NOT used**
> - Containers use [PostgresContainerSettings](Contexts/Postgres/PostgresContainerSettings.cs) defaults:
>   - `DefaultPassword = "drn"`
>   - `Username = "drn"` (from `DbContextConventions.DefaultUsername`)
>   - `Database = "drn"` (from `DbContextConventions.DefaultDatabase`)
> - Connection strings are automatically injected into configuration

---

### Configuration Settings Reference

#### Settings for Docker/Kubernetes Development

These settings are used **only** by [DrnContextDevelopmentConnection](../DRN.Framework.EntityFramework/Context/DrnContextDevelopmentConnection.cs) for containerized development. They are **NOT used** by `ContainerContext` or `LaunchExternalDependencies`.

| Setting | Default | Source |
|---------|---------|--------|
| `DrnContext_DevHost` | `drn` | DbContextConventions.DevHostKey |
| `DrnContext_DevPort` | `5432` | DbContextConventions.DevPortKey |
| `DrnContext_DevUsername` | `drn` | DbContextConventions.DevUsernameKey |
| `DrnContext_DevDatabase` | `drn` | DbContextConventions.DevDatabaseKey |
| `postgres-password` | *(required)* | DbContextConventions.DevPasswordKey |

#### Migration and Workflow Settings
Usually set by appsettings.Development.json, environment variables, config maps.

| Setting | Default | Source | Purpose |
|---------|---------|--------|---------|
| `DrnDevelopmentSettings:AutoMigrate` | `false` | DrnDevelopmentSettings.AutoMigrate | Enables migration flow |
| `DrnDevelopmentSettings:Prototype` | `false` | DrnDevelopmentSettings.Prototype | Enable DB recreation on model changes |
| `DrnDevelopmentSettings:LaunchExternalDependencies` | `false` | DrnDevelopmentSettings.LaunchExternalDependencies | Launch Testcontainers |
| `DrnDevelopmentSettings:TemporaryApplication` | `false` | DrnDevelopmentSettings.TemporaryApplication | **Auto-set by tests** to prevent collision |

---

### DrnDevelopmentSettings in Tests

`ApplicationContext` automatically manages flags to ensure test isolation:

```csharp
// Automatically set by ApplicationContext.CreateApplication<TProgram>()
TestEnvironment.DrnTestContextEnabled = true;
AppSettings.DevelopmentSettings.TemporaryApplication = true;
```

**What This Means**:
- **`TemporaryApplication = true`**: Prevents test runs from interfering with local dev containers
- **`DrnTestContextEnabled = true`**: Signals test context, enabling test-specific behaviors


See [DrnDevelopmentSettings.cs](../DRN.Framework.Utils/Settings/DrnDevelopmentSettings.cs) for the complete class definition.

## Data Attributes
DRN.Framework.Testing provides following data attributes that can provide data to tests:
* DataInlineAttribute
* DataMemberAttribute
* DataSelfAttribute

Following design principle is used for these attributes
* All attributes have data prefix to benefit from autocomplete
* All data attributes automatically provide `DrnTestContext` as first parameter if tests requires
* All data attributes try to provide missing values with AutoFixture and NSubstitute
* All data attributes will automatically override DrnTestContext's service collection with provided NSubstitute interfaces 
* DataInline attribute works like xunit `InlineData` except they try to provide missing values with AutoFixture and NSubstitute
* DataMember attribute works like xunit `MemberData` except they try to provide missing values with AutoFixture and NSubstitute
* DateSelf attribute needs to be inherited by another class and should call `AddRow` method in constructor to provide data

Example usages for DataMember attribute
```csharp
[Theory]
[DataMember(nameof(DrnTestContextInlineMemberData))]
public void DrnTestContextMember_Should_Inline_And_Auto_Generate_Missing_Test_Data(DrnTestContext testContext,
    int inline, ComplexInline complexInline, Guid autoGenerate, IMockable mock)
{
    testContext.Should().NotBeNull();
    testContext.TestMethod.Name.Should().Be(nameof(DrnTestContextMember_Should_Inline_And_Auto_Generate_Missing_Test_Data));
    inline.Should().BeGreaterThan(10);
    complexInline.Count.Should().BeLessThan(10);
    autoGenerate.Should().NotBeEmpty();
    mock.Max.Returns(75);
    mock.Max.Should().Be(75);
}

public static IEnumerable<object[]> DrnTestContextInlineMemberData => new List<object[]>
{
    new object[] { 11, new ComplexInline(8) },
    new object[] { int.MaxValue, new ComplexInline(-1) }
};
```

Example usage for DataSelf attribute
```csharp
public class DataSelfContextAttributeTests
{
    [Theory]
    [DataSelfContextTestData]
    public void DrnTestContextClassData_Should_Inline_And_Auto_Generate_Missing_Test_Data(DrnTestContextUnit testContext,
        int inline, ComplexInline complexInline, Guid autoGenerate, IMockable mock)
    {
        testContext.Should().NotBeNull();
        testContext.MethodContext.TestMethod.Name.Should().Be(nameof(DrnTestContextClassData_Should_Inline_And_Auto_Generate_Missing_Test_Data));
        inline.Should().BeGreaterThan(98);
        complexInline.Count.Should().BeLessThan(1001);
        autoGenerate.Should().NotBeEmpty();
        mock.Max.Returns(44);
        mock.Max.Should().Be(44);
    }
}

public class DataSelfContextTestData : DataSelfUnitAttribute
{
    public DataSelfContextTestData()
    {
        AddRow(99, new ComplexInline(100));
        AddRow(199, new ComplexInline(1000));
    }
}
```

Example usage for DataInline attribute
```csharp
[Theory]
[DataInline(99)]
public void TextContext_Should_Be_Created_From_DrnTestContextData(DrnTestContext context, int inlineData, Guid autoInlinedData, IMockable autoInlinedMockable)
{
    inlineData.Should().Be(99);
    autoInlinedData.Should().NotBeEmpty(); //guid generated by AutoFixture
    autoInlinedMockable.Max.Returns(int.MaxValue); //dependency mocked by NSubstitute

    context.ServiceCollection.AddApplicationServices(); //you can add services, modules defined in hosted app, application, infrastructure layer etc..
    var serviceProvider = context.BuildServiceProvider(); //appsettings.json added by convention. Context and service provider will be disposed by xunit
    serviceProvider.GetService<ToBeRemovedService>().Should().BeNull(); //Service provider behaviour demonstration

    var dependentService = serviceProvider.GetRequiredService<DependentService>();
    dependentService.Max.Should().Be(int.MaxValue);
}
```

## Unit Testing

For pure unit tests where you don't need the full dependency injection container or container orchestration, use `DrnTestContextUnit` and the corresponding **Unit** attributes.

### Unit Attributes
* `[DataInlineUnit]`: Same as `DataInline` but provides `DrnTestContextUnit`.
* `[DataMemberUnit]`: Same as `DataMember` but provides `DrnTestContextUnit`.
* `[DataSelfUnit]`: Same as `DataSelf` but provides `DrnTestContextUnit`.

### DrnTestContextUnit
Unlike `DrnTestContext`, `DrnTestContextUnit` is lightweight and focused on Method Context (managing test data and method info) without the overhead of `ServiceCollection` or `ContainerContext`.

```csharp
[Theory]
[DataInlineUnit(99)]
public void Unit_Test_Example(DrnTestContextUnit context, int value, IMockable mock)
{
    // Fast, lightweight, no container overhead
    context.MethodContext.MethodName.Should().Be(nameof(Unit_Test_Example));
    
    mock.Max.Returns(value);
    var service = new DependentService(mock); // Manually inject dependencies
    
    service.Max.Should().Be(99);
}
```

## DebugOnly Tests
Following attributes can be used to run test only when the debugger is attached. These attributes does respect the attached debugger, not debug or release configuration.
* FactDebuggerOnly
* TheoryDebuggerOnly

## DI Health Validation

Use `ValidateServicesAsync()` to catch missing dependency registrations before they fail your application at runtime.

```csharp
[Theory]
[DataInline]
public async Task Dependency_Injection_Should_Be_Healthy(DrnTestContext context)
{
    context.ServiceCollection.AddApplicationServices();
    
    // Verifies that all registered services can be successfully resolved
    await context.ValidateServicesAsync();
}
```

## JSON Utilities

The `JsonObjectExtensions` provide a simple way to verify API contracts and serialization stability.

### ValidateObjectSerialization

Ensures that an object can be serialized to JSON and deserialized back to an equivalent object.

```csharp
[Theory]
[DataInline]
public void Contract_Should_RoundTrip_Successfully(MyContractDto dto)
{
    // AutoFixture fills dto, then we verify round-trip
    dto.ValidateObjectSerialization();
}
```

## FlurlHttpTest Integration

`DrnTestContext` provides built-in support for mocking HTTP requests via [Flurl.Http.Testing](https://flurl.dev/docs/testable-http/). This enables testing services that make external API calls without hitting real endpoints.

### Basic Usage

```csharp
[Theory]
[DataInline]
public async Task External_API_Should_Be_Mocked(DrnTestContext context)
{
    // Setup mock response
    context.FlurlHttpTest.RespondWith("{ \"status\": \"ok\" }", 200);
    
    context.ServiceCollection.AddSingleton<IExternalApiClient, ExternalApiClient>();
    var client = context.GetRequiredService<IExternalApiClient>();
    
    var result = await client.GetStatusAsync();
    
    result.Status.Should().Be("ok");
    
    // Verify the request was made
    context.FlurlHttpTest.ShouldHaveCalled("https://api.example.com/status")
        .WithVerb(HttpMethod.Get)
        .Times(1);
}
```

### Simulating Failures

```csharp
[Theory]
[DataInline]
public async Task Service_Should_Handle_API_Failure(DrnTestContext context)
{
    // Simulate server error
    context.FlurlHttpTest.RespondWith(status: 500);
    
    context.ServiceCollection.AddSingleton<IExternalApiClient, ExternalApiClient>();
    var client = context.GetRequiredService<IExternalApiClient>();
    
    var act = async () => await client.GetStatusAsync();
    
    await act.Should().ThrowAsync<FlurlHttpException>();
}
```

### Sequential Responses

```csharp
[Theory]
[DataInline]
public async Task Retry_Logic_Should_Work(DrnTestContext context)
{
    // First call fails, second succeeds (testing retry logic)
    context.FlurlHttpTest
        .RespondWith(status: 503)
        .RespondWith("{ \"status\": \"ok\" }", 200);
    
    // ... test retry behavior
}
```

## Providers
### SettingsProvider
`SettingsProvider` gets the settings from Settings folder. Settings file path is relative Settings folder. Settings folder must be created in the root of the test Project. Make sure the settings file is copied to output directory.
```csharp
    [Fact]
    public void SettingsProvider_Should_Return_IAppSettings_Instance()
    {
        var appSettings = SettingsProvider.GetAppSettings();

        appSettings.GetRequiredSection("AllowedHosts").Value.Should().Be("*");
        appSettings.TryGetSection("Bar", out _).Should().BeTrue();
        appSettings.TryGetSection("Foo", out _).Should().BeFalse();
        appSettings.GetRequiredConnectionString("Foo").Should().Be("Bar");
        appSettings.TryGetConnectionString("Bar", out _).Should().BeFalse();
    }

    [Fact]
    public void SettingsProvider_Should_Return_IConfiguration_Instance()
    {
        var configuration = SettingsProvider.GetConfiguration("secondaryAppSettings");

        configuration.GetRequiredSection("AllowedHosts").Value.Should().Be("*");
        configuration.GetSection("Foo").Exists().Should().BeTrue();
        configuration.GetSection("Bar").Exists().Should().BeFalse();
        configuration.GetConnectionString("Bar").Should().Be("Foo");
    }
```
### DataProvider
`DataProvider` gets the content of specified data file in the Data folder. Data file path is relative Data folder including file extension. Data folder must be created in the root of the test Project. Make sure the data file is copied to output directory.
```csharp
    [Fact]
    public void DataProvider_Should_Return_Data_From_Test_File()
    {
        DataProvider.Get("Test.txt").Should().Be("Foo");
    }
```

### CredentialsProvider
`CredentialsProvider` is a helper class for generating and caching test usernames and passwords.
```csharp
    [Fact]
    public void CredentialsProvider_Should_Generate_Test_User()
    {
        var credentials = CredentialsProvider.GenerateCredentials();
        credentials.Username.Should().StartWith("testuser_");
        credentials.Password.Length.Should().BeGreaterThanOrEqualTo(12);
    }
```
### xUnit Runner Configuration
`xunit.runner.json` is optional but recommended for configuring the test runner. When using **Microsoft Testing Platform (MTP)**, this file ensures the runner behaves as expected (e.g., parallelization settings). Ensure this file is set to `CopyToOutputDirectory` in your csproj.

```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "diagnosticMessages": true,
  "parallelizeAssembly": true,
  "parallelizeTestCollections": true
}
```

## Example Test Project .csproj File
Don't forget to replace DRN.Framework.Testing project reference with its nuget package reference
```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
        <OutputType>Exe</OutputType>
        <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="xunit.v3.mtp-v2" Version="3.2.2" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\DRN.Framework.Testing\DRN.Framework.Testing.csproj"/>
    </ItemGroup>

    <ItemGroup>
        <None Update="Settings\defaultAppSettings.json">
            <CopyToOutputDirectory>Always</CopyToOutputDirectory>
        </None>
        <None Update="Data\Test.txt">
            <CopyToOutputDirectory>Always</CopyToOutputDirectory>
        </None>
        <None Update="Settings\secondaryAppSettings.json">
          <CopyToOutputDirectory>Always</CopyToOutputDirectory>
        </None>
        <None Update="xunit.runner.json">
            <CopyToOutputDirectory>Always</CopyToOutputDirectory>
        </None>
    </ItemGroup>

</Project>
```

## Test Snippet

**dtt** snippet for creating tests with a test context.
```csharp
[Theory]
[DataInline]
public async Task $name$(DrnTestContext context)
{
    $END$
}
```

## Testing Guide and DTT Approach

DTT(Duran's Testing Technique) is developed upon following two ideas to make testing natural part of the software development:
* Writing a unit or integration test, providing settings and data to it should be easy, effective and encouraging as much as possible
* A test should test actual usage as much as possible.

DTT with **DrnTestContext** makes these ideas possible by
* being aware of test data and location
* effortlessly providing test data and settings
* effortlessly providing service collection
* effortlessly providing service provider
* effortlessly validating service provider
* effortlessly wiring external dependencies with Container Context
* effortlessly wiring application with Application Context
With the help of test context, integration tests can be written easily with following styles.
1. A data context attribute can provide NSubstituted interfaces and test context automatically replaces actual implementations with mocked interfaces and provides test data.
2. Test containers can be used as actual dependencies instead of mocking them.
3. With FactDebuggerOnly and TheoryDebuggerOnly attributes, cautiously written tests can use real databases and dependencies to debug production usage.

### Comparison

**Without DTT:**
Manual setup requires significant boilerplate to achieve dependency injection, mocking, and data generation.

```csharp
[Fact]
public void Manual_Setup_Boilerplate_Fatigue()
{
    // 1. Setup DI container
    var services = new ServiceCollection();
    
    // 2. Manual Mocking
    var mockDependency = Substitute.For<IMockable>();
    mockDependency.Max.Returns(99);
    services.AddSingleton(mockDependency);
    services.AddTransient<DependentService>();

    // 3. Manual Data Generation
    var fixture = new Fixture();
    var autoGeneratedId = fixture.Create<Guid>();

    // 4. Build Provider
    var serviceProvider = services.BuildServiceProvider();
    var systemUnderTest = serviceProvider.GetRequiredService<DependentService>();

    // 5. Test logic...
}
```

**With DTT:**
The same setup is handled declaratively, allowing you to focus immediately on the test logic.

```csharp
[Theory]
[DataInline(99)]
public void DTT_Pit_Of_Success(DrnTestContext context, int value, IMockable mockDependency, Guid autoGeneratedId)
{
    // DI, Mocking, and Data Generation are already done.
    
    mockDependency.Max.Returns(value);
    
    var systemUnderTest = context.GetRequiredService<DependentService>();
    // ...
}
```

**Scenario 2: Integration Testing (Containers + WebApp + Migrations)**

**Without DTT:**
Setting up a realistic integration test with Testcontainers, EF Core Migrations, and WebApplicationFactory requires understanding the lifecycle of multiple complex components.

```csharp
public class Complex_Integration_Test : IAsyncLifetime
{
    private PostgreSqlContainer _container;
    private WebApplicationFactory<Program> _factory;

    public async Task InitializeAsync()
    {
        // 1. Spin up Container
        _container = new PostgreSqlBuilder().WithImage("postgres:15").Build();
        await _container.StartAsync();

        // 2. Configure WebApp to use Container
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new[]
                    {
                        new KeyValuePair<string, string>("ConnectionStrings:Default", _container.GetConnectionString())
                    });
                });
            });

        // 3. Apply Migrations Manually
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MyDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    [Fact]
    public async Task Manual_Integration_Pain()
    {
        var client = _factory.CreateClient();
        // Test logic...
    }

    public async Task DisposeAsync()
    {
        await _factor.DisposeAsync();
        await _container.DisposeAsync();
    }
}
```

**With DTT:**
DrnTestContext handles the entire lifecycle orchestration for you.

```csharp
[Theory]
[DataInline]
public async Task DTT_Full_Integration_Magic(DrnTestContext context, ITestOutputHelper output)
{
    // One line to rule them all:
    // 1. Starts Postgres & RabbitMQ containers
    // 2. Wires up connection strings to overrides
    // 3. Applies EF Core Migrations
    // 4. Bootstraps WebApplicationFactory
    
    var client = await context.ApplicationContext.CreateClientAsync<Program>(output);
    
    // Ready to test immediately
}
```

### Willpower Depletion

Willpower is a finite daily resource. When testing requires high activation energy (manually wiring dependencies, managing container lifecycles, and writing boilerplate), it consumes this resource rapidly. This leads to "testing fatigue," where developers subconsciously avoid writing tests or delay them until the last possible moment.

DTT is designed to minimize this cognitive cost. By making the "correct" way to test (isolated, realistic, dependency-injected) also the **easiest** way to test, it preserves your willpower for solving business problems. 

This encapsulates the core philosophy of **"The Pit of Success"** - a concept where the system design guides you to the right thing naturally, rather than fighting you.

*   **The "Disciplined Chore" (The Old Way)**: Usually, writing high-quality software tests (especially integration tests) requires friction. You have to manually spin up containers, wipe databases, and wire up DI boilerplate. Because this is hard and tedious, doing it correctly requires *Discipline*. When you are tired or under a deadline, that discipline fails, and you stop writing good tests.
*   **The "Path of Least Resistance" (The Human Nature)**: Humans are wired to conserve energy and gravitate towards the easiest possible action. Traditionally, the "Path of Least Resistance" is not writing tests or writing bad, fragile tests just to get it over with.
*   **The DTT Transformation**: DTT flips this equation. By wrapping all that complex rigour (containers, migrations, DI, and auto-mocking) into a single attribute (`[DataInline]`), it makes the **Result** (High Quality Test) accessible via the **Action** (Lowest Effort).

It makes the "Right Thing" easier than the "Wrong Thing." You no longer need discipline to write great tests; you just need to follow the easiest path available to you.

With DTT, software testing becomes a natural part of software development.

## Global Usings

```csharp
global using Xunit;
global using Xunit.v3;
global using AutoFixture;
global using AutoFixture.AutoNSubstitute;
global using AutoFixture.Xunit3;
global using AwesomeAssertions;
global using NSubstitute;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Configuration;
global using DRN.Framework.Testing;
global using DRN.Framework.Testing.Contexts;
global using DRN.Framework.Testing.DataAttributes;
global using DRN.Framework.Testing.Providers;
global using DRN.Framework.Testing.TestAttributes;
global using DRN.Framework.Utils.Extensions;
global using DRN.Framework.Utils.Settings;
global using DRN.Framework.SharedKernel;
global using DRN.Framework.Utils.DependencyInjection;
global using System.Reflection;
global using System.IO;
global using System.Linq;
global using System.Collections;
global using Xunit.Abstractions;
```

## Telemetry Opt-Out

Add the following to your shell profile (e.g., `~/.zshrc` or `~/.bashrc`) to opt out of telemetry:

```bash
# Opt out of .NET CLI telemetry
export DOTNET_CLI_TELEMETRY_OPTOUT=1

# Opt out of .NET Testing Platform telemetry
export TESTINGPLATFORM_TELEMETRY_OPTOUT=1
```

**References:**
- [.NET SDK Telemetry](https://learn.microsoft.com/en-us/dotnet/core/tools/telemetry)
- [Microsoft.Testing.Platform Telemetry](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-telemetry)

---

## Related Packages

- [DRN.Framework.SharedKernel](https://www.nuget.org/packages/DRN.Framework.SharedKernel/) - Domain primitives and exceptions
- [DRN.Framework.Utils](https://www.nuget.org/packages/DRN.Framework.Utils/) - Configuration and DI utilities
- [DRN.Framework.EntityFramework](https://www.nuget.org/packages/DRN.Framework.EntityFramework/) - EF Core integration
- [DRN.Framework.Hosting](https://www.nuget.org/packages/DRN.Framework.Hosting/) - Web application hosting

For complete examples, see [Sample.Hosted](https://github.com/duranserkan/DRN-Project/tree/master/Sample.Hosted).

---

Documented with the assistance of [DiSC OS](https://github.com/duranserkan/DRN-Project/blob/develop/DiSCOS/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**
