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

DRN.Framework.EntityFramework provides DrnContext with conventions to develop rapid and effective domain models.

## DRNContext

DrnContext has following unique features:

* Implements `IDesignTimeDbContextFactory` to enable migrations from dbContext defining projects.
* Implements `IDesignTimeServices` to support multi context projects with default output directories in the context specific folder.
* Uses `HasDrnContextServiceCollectionModule` attribute for automatic registration with AddServicesWithAttributes service collection extension method.
* Uses context name (typeof(TContext).Name) as connection string key by convention.
* Automatically applies `IEntityTypeConfiguration` implementations from the assembly whose namespace contains the derived context's namespace.
* Automatically marks Entities derived from `DRN.Framework.SharedKernel.Domain.Entity` as created, modified or deleted.
* Enables `DRN.Framework.Testing` to create easy and effective integration tests with conventions and automatic registrations.
    * Application modules can be registered without any modification to `TestContext`
    * `TestContext`'s `ContainerContext`
        * creates a `postgresql container` then scans TestContext's service collection for inherited DrnContexts.
        * Adds a connection strings to TestContext's configuration for each `DrnContext` according to convention.
    * `TestContext` acts as a ServiceProvider and when a service is requested it can build it from service collection with all dependencies.

```csharp
namespace DRN.Framework.EntityFramework.Context;

[HasDrnContextServiceCollectionModule]
public abstract class DrnContext<TContext> : DbContext, IDesignTimeDbContextFactory<TContext>, IDesignTimeServices where TContext : DbContext, new()
{ 
...

public class HasDrnContextServiceCollectionModuleAttribute : HasServiceCollectionModuleAttribute
{
    static HasDrnContextServiceCollectionModuleAttribute()
    {
        ModuleMethodInfo = typeof(ServiceCollectionExtensions).GetMethod(nameof(ServiceCollectionExtensions.AddDbContextsWithConventions))!;
    }
}
```

### Example Usage

```csharp
namespace Sample.Infra;

public static class InfraModule
{
    public static IServiceCollection AddSampleInfraServices(this IServiceCollection sc)
    {
        sc.AddServicesWithAttributes();

        return sc;
    }
}

public class QAContext : DrnContext<QAContext>
{
    public QAContext(DbContextOptions<QAContext> options) : base(options)
    {
    }

    public QAContext() : base(null)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<Answer> Answers { get; set; }
    public DbSet<QuestionComment> Comments { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Tag> Tags { get; set; }
}
```

### Development Environment Configurations
Following configuration options added to minimize development environment creation efforts:
* DrnContext development connection string will be auto generated when
    * `Environment` configuration key set as Development and,
    * `DrnContext_DevPassword` configuration key set and,
    * No other connection string is provided for the DbContexts.
* Following keys can set optionally according to DbContextConventions;
    * `DrnContext_AutoMigrateDevEnvironment`
        * When set true applies migrations automatically
    * `DrnContext_DevHost`
    * `DrnContext_DevPort`
    * `DrnContext_DevUsername`
        * default is postgres
    * `DrnContext_DevDatabase`
        * default is drnDb

`DrnContext_DevPassword` and `DrnContext_AutoMigrateDevEnvironment` should be enough to start a hosted service that has DrnContext dependencies. 

For instance: 
 * When a Postgresql helm chart is used for dev environment and it creates a password secret automatically,
 * Then only defining a volume mount should be enough for database configuration.

### Global Usings

```csharp
global using DRN.Framework.EntityFramework.Context;
global using Microsoft.EntityFrameworkCore;
global using DRN.Framework.Utils.DependencyInjection;
```