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

## Introduction
DRN.Framework.Testing package provides practical, effective helpers such as resourceful data attributes and test context.

This package enables a new encouraging testing technique called as DTT(Duran's Testing Technique). 
With DTT, any developer can write clean and hassle-free unit and integration tests without complexity.

### QuickStart: Basics
Here's a basic test demonstration to take your attention and get you started:
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

### Table of Contents
* Introduction
* DrnTestContext
* ContainerContext
* WebApplicationContext
* DataAttributes
* DebugOnly Tests
* Settings and Data Providers
* Global Usings
* Example Test Project
* Test snippet
* Testing guide and DTT approach

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

### QuickStart: Advanced Data Inline
* `DataInline` will provide `DrnTestContext` as first parameter. 
* Then it will provide inlined values.
* Then it will provide auto inline missing values with AutoFixture.
* `AutoFixture` will mock any interface requested with `NSubstitute`.
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
  * can start a `postgres` container, apply migrations for dbContexts derived from DrnContext and updates connection string configuration with a single line of code
* provides `WebApplicationContext`
  * syncs `DrnTestContext` service collection and service provider with provided application by WebApplicationFactory
* provides `IConfiguration` and `IAppSettings` with SettingsProvider by using convention.
  * settings.json file can be found in the same folder with test
  * settings.json file can be found in the global Settings folder or Settings folder that stays in the test folder
  * Make sure file is copied to output directory
  * If no settings file is specified while calling `BuildServiceProvider`. `appsettings.json` file be searched by convention.
* provides data file contents by using convention.
  * data file can be found in the same folder with test
  * data file can be found in the global Data folder or Data folder that stays in the test folder
  * Make sure file is copied to output directory
* `ServiceProvider` provides utils provided with DRN.Framework.Utils' `UtilsModule`
* `BuildServiceProvider` replaces dependencies that can be replaced with inlined interfaces.
* `ServiceProvider` and `DrnTestContext` will be disposed by xunit when test finishes

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
With `ContainerContext` and conventions you can easily write effective integration tests against your database dependencies 
```csharp
    [Theory]
    [DataInline]
    public async Task QAContext_Should_Add_Category(DrnTestContext context)
    {
        context.ServiceCollection.AddSampleInfraServices();
        await context.ContainerContext.StartPostgresAndApplyMigrationsAsync();
        var qaContext = context.GetRequiredService<QAContext>();

        var category = new Category("dotnet8");
        qaContext.Categories.Add(category);
        await qaContext.SaveChangesAsync();
        category.Id.Should().BePositive();
    }
```
* Application modules can be registered without any modification to `DrnTestContext`
* `DrnTestContext`'s `ContainerContext`
  * creates a `postgresql container` then scans DrnTestContext's service collection for inherited DrnContexts.
  * Adds a connection strings to DrnTestContext's configuration for each derived `DrnContext` according to convention.
* `DrnTestContext` acts as a ServiceProvider and when a service is requested it can build it from service collection with all dependencies.

## WebApplicationContext
`WebApplicationContext` syncs `DrnTestContext` service collection and service provider with provided application by WebApplicationFactory.
* You can provide or override configurations and services to your program until you force `WebApplicationFactory` to build a `Host` such as creating `HttpClient` or requesting `TestServer`.
```csharp
    [Theory]
    [DataInline]
    public async Task WebApplicationContext_Should_Provide_Configuration_To_Program(DrnTestContext context)
    {
        var webApplication = context.WebApplicationContext.CreateWebApplication<Program>();
        await context.ContainerContext.StartPostgresAndApplyMigrationsAsync();
        
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
    DrnTestContext.Should().NotBeNull();
    DrnTestContext.TestMethod.Name.Should().Be(nameof(DrnTestContextMember_Should_Inline_And_Auto_Generate_Missing_Test_Data));
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
    public void DrnTestContextClassData_Should_Inline_And_Auto_Generate_Missing_Test_Data(DrnTestContext testContext,
        int inline, ComplexInline complexInline, Guid autoGenerate, IMockable mock)
    {
        DrnTestContext.Should().NotBeNull();
        DrnTestContext.TestMethod.Name.Should().Be(nameof(DrnTestContextClassData_Should_Inline_And_Auto_Generate_Missing_Test_Data));
        inline.Should().BeGreaterThan(98);
        complexInline.Count.Should().BeLessThan(1001);
        autoGenerate.Should().NotBeEmpty();
        mock.Max.Returns(44);
        mock.Max.Should().Be(44);
    }
}

public class DataSelfContextTestData : DataSelfContextAttribute
{
    public DataSelfContextTestData1()
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

## DebugOnly Tests
Following attributes can be used to run test only when the debugger is attached. These attributes does respect the attached debugger, not debug or release configuration.
* FactDebuggerOnly
* TheoryDebuggerOnly

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
## Global Usings
Following global usings can be used in a `Usings.cs` file in test projects to reduce line of code in test files
```csharp
global using Xunit;
global using AutoFixture;
global using AutoFixture.AutoNSubstitute;
global using AutoFixture.Xunit2;
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
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Configuration;
global using NSubstitute;
global using System.Reflection;
global using System.IO;
global using System.Linq;
global using System.Collections;
```
## Example Test Project .csproj File
Don't forget to replace DRN.Framework.Testing project reference with its nuget package reference
```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net7.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.7.2"/>
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
    </ItemGroup>

</Project>
```

##  Test snippet

**dtt** snippet for creating tests with a test context.
```csharp
[Theory]
[DataInline]
public async Task $name$(DrnTestContext context)
{
    $END$
}
```

## Testing guide and DTT approach

DTT(Duran's Testing Technique) is developed upon following 2 idea to make testing natural part of the software development:
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
s
With the help of test context, integration tests can be written easily with following styles.
1. A data context attribute can provide NSubstituted interfaces and test context automatically replaces actual implementations with mocked interfaces and provides test data.
2. Test containers can be used as actual dependencies instead of mocking them.
3. With FactDebuggerOnly and TheoryDebuggerOnly attributes, cautiously written tests can use real databases and dependencies to debug production usage.

With DTT, software testing becomes natural part of the software development.

---
**Semper Progressivus: Always Progressive**