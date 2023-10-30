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

This package enables a new encouraging testing technique called as DTT(Duran's Testing Technique). With DTT, any developer can write clean and hassle-free unit and integration tests without complexity.

### Encapsulated Packages
You no longer need to be reference followings in your test project:
* AutoFixture.AutoNSubstitute
* AutoFixture.Xunit2
* DRN.Framework.Utils
* FluentAssertions
* NSubstitute
* xunit

### QuickStart: Basics
Here's a basic test demonstration to take your attention and get you started:
```csharp
    [Theory]
    [DataInlineContext]
    public void DataInlineContextDemonstration(TestContext context, IMockable autoInlinedDependency)
    {
        context.ServiceCollection.AddApplicationServices();
        //Context wraps service provider and automagically replaces actual dependencies with auto inlined dependencies
        var dependentService = context.GetRequiredService<DependentService>();
        
        autoInlinedDependency.Max.Returns(int.MaxValue); //dependency is mocked by NSubstitute
        dependentService.Max.Should().Be(int.MaxValue); //That is all. It is clean and effective 
    }
```

### Table of Contents
* Introduction
* TestContext
* DataAttributes
* DebugOnly Tests
* Settings and Data Providers
* Global Usings
* Example Test Project

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
* `DataInlineContext` will provide `TestContext` as first parameter. 
* Then it will provide inlined values.
* Then it will provide auto inline missing values with AutoFixture.
* `AutoFixture` will mock any interface requested with `NSubstitute`.
```csharp
/// <param name="context"> Provided by DataInlineContext even if it is not a compile time constant</param>
/// <param name="inlineData">Provided by DataInlineContext</param>
/// <param name="autoInlinedData">DataInlineContext will provide missing data with the help of AutoFixture</param>
/// <param name="autoInlinedMockable">DataInlineContext will provide implementation mocked by NSubstitute</param>
[Theory]
[DataInlineContext(99)]
public void TextContext_Should_Be_Created_From_TestContextData(TestContext context, int inlineData, Guid autoInlinedData, IMockable autoInlinedMockable)
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

## TestContext
`TestContext` has following properties:
* captures values provided to running test method and its method info.
* provides `ServiceCollection` so that to be tested services and dependencies can be added before building `ServiceProvider`.
* provides lightweight `ServiceProvider` that contains default logging without any provider
  * `ServiceProvider` can provide services that depends like `ILogger<DefaultService>`
  * logged data will not be leaked to anywhere since it has no logging provider.
* `ServiceProvider` provides `IConfiguration` and `IAppSettings` with SettingsProvider.
  * SettingsProvider reads json settings files that can be found in the settings folder of the test project
  * Make sure file is copied to output directory
  * If no settings file is specified while calling `BuildServiceProvider`. `appsettings.json` file be searched by convention.
* `ServiceProvider` provides utils provided with DRN.Framework.Utils' `UtilsModule`
* `BuildServiceProvider` replaces dependencies that can be replaced with inlined interfaces.
* `ServiceProvider` and `TestContext` will be disposed by xunit when test finishes

## Data Attributes
DRN.Framework.Testing provides following data attributes that can provide data to tests:
* DataInlineAutoAttribute
* DataInlineContextAttribute
* DataMemberAutoAttribute
* DataMemberContextAttribute
* DataSelfAutoAttribute
* DataSelfContextAttribute

Following design principle is used for these attributes
* All attributes has data prefix to benefit from autocomplete
* Inline attributes works like xunit `InlineData` except they try to provide missing values with AutoFixture and NSubstitute
* Member attributes works like xunit `MemberData` except they try to provide missing values with AutoFixture and NSubstitute
* Self attributes needs to be inherited by another class and should call `AddRow` method in constructor to provide data
* Context attributes provide `TestContext` as first parameter

### Member Attributes
Example usages for member attributes
```csharp
[Theory]
[DataMemberAuto(nameof(DataMemberAutoData))]
public void AutoMember_Should_Inline_And_Auto_Generate_Missing_Test_Data(int inline, ComplexInline complexInline, Guid autoGenerate, IMockable mock)
{
    inline.Should().BeGreaterThan(10);
    complexInline.Count.Should().BeLessThan(10);
    autoGenerate.Should().NotBeEmpty();
    mock.Max.Returns(75);
    mock.Max.Should().Be(75);
}

public static IEnumerable<object[]> DataMemberAutoData => new List<object[]>
{
    new object[] { 11, new ComplexInline(8) },
    new object[] { int.MaxValue, new ComplexInline(-1) }
};
```
```csharp
[Theory]
[DataMemberContext(nameof(TestContextInlineMemberData))]
public void TestContextMember_Should_Inline_And_Auto_Generate_Missing_Test_Data(TestContext testContext,
    int inline, ComplexInline complexInline, Guid autoGenerate, IMockable mock)
{
    testContext.Should().NotBeNull();
    testContext.TestMethod.Name.Should().Be(nameof(TestContextMember_Should_Inline_And_Auto_Generate_Missing_Test_Data));
    inline.Should().BeGreaterThan(10);
    complexInline.Count.Should().BeLessThan(10);
    autoGenerate.Should().NotBeEmpty();
    mock.Max.Returns(75);
    mock.Max.Should().Be(75);
}

public static IEnumerable<object[]> TestContextInlineMemberData => new List<object[]>
{
    new object[] { 11, new ComplexInline(8) },
    new object[] { int.MaxValue, new ComplexInline(-1) }
};
```

### Self Attributes
Example usages for self attributes
```csharp
public class DataSelfAutoAttributeTests
{
    [Theory]
    [DataSelfAutoTestData]
    public void AutoClass_Should_Inline_And_Auto_Generate_Missing_Test_Data(int inline, ComplexInline complexInline, Guid autoGenerate, IMockable mock)
    {
        inline.Should().BeGreaterThan(10);
        complexInline.Count.Should().BeLessThan(10);
        autoGenerate.Should().NotBeEmpty();
        mock.Max.Returns(75);
        mock.Max.Should().Be(75);
    }
}

public class DataSelfAutoTestData : DataSelfAutoAttribute
{
    public DataSelfAutoTestData()
    {
        AddRow(200, new ComplexInline(9));
        AddRow(300, new ComplexInline(int.MinValue));
    }
}
```
```csharp
public class DataSelfContextAttributeTests
{
    [Theory]
    [DataSelfContextTestData]
    public void TestContextClassData_Should_Inline_And_Auto_Generate_Missing_Test_Data(TestContext testContext,
        int inline, ComplexInline complexInline, Guid autoGenerate, IMockable mock)
    {
        testContext.Should().NotBeNull();
        testContext.TestMethod.Name.Should().Be(nameof(TestContextClassData_Should_Inline_And_Auto_Generate_Missing_Test_Data));
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
        AddRow(99,new ComplexInline(100));
        AddRow(199,new ComplexInline(1000));
    }
}
```

### Inline Attributes
Example usages for inline attributes
```csharp
[Theory]
[DataInlineAuto(10)]
public void AutoInline_Should_Inline_And_Auto_Generate_Missing_Test_Data(int inline, Guid autoGenerate, IMockable mock)
{
    inline.Should().Be(10);
    autoGenerate.Should().NotBeEmpty();
    mock.Max.Returns(65);
    mock.Max.Should().Be(65);
}
```
```csharp
[Theory]
[DataInlineContext(99)]
public void TextContext_Should_Be_Created_From_TestContextData(TestContext context, int inlineData, Guid autoInlinedData, IMockable autoInlinedMockable)
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
global using DRN.Framework.Testing;
global using DRN.Framework.Testing.DataAttributes;
global using DRN.Framework.Testing.Providers;
global using DRN.Framework.Testing.TestAttributes;
global using FluentAssertions;
global using Microsoft.Extensions.DependencyInjection;
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