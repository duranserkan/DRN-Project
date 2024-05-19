Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.4.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to 19 May Commemoration of Atatürk, Youth and Sports Day.

### Breaking Changes

* DrnContext and DrnContextIdentity MigrationsHistoryTable database schema changes as __entity_migrations
  * Each DbContext's history table is named according to following convention:
    * {contextName.ToSnakeCase()}_history
* DrnContext and DrnContextIdentity applies context name with snake case formatting as default schema name.

### New Features

* DrnContextIdentity added to support ASP.NET Core Identity  with DrnContext features
  * DrnContextIdentity to inherits IdentityDbContext

### Bug Fixes

* Microsoft.EntityFrameworkCore.Tools PrivateAssets were preventing migration

## Version 0.3.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to 23 April National Sovereignty and Children's Day.

### Breaking Changes

### New Features

* DrnContext development connection string will be auto generated when
    * Environment configuration key set as Development and,
    * postgres-password configuration key set and,
    * No other connection string is provided for the DbContexts.
* Following keys can set optionally according to DbContextConventions;
    * DrnContext_AutoMigrateDevEnvironment
        * When set true applies migrations automatically
    * DrnContext_DevHost
    * DrnContext_DevPort
    * DrnContext_DevUsername
        * default is postgres
    * DrnContext_DevDatabase
        * default is drnDb

### Bug Fixes

## Version 0.2.0

### Breaking Changes

### New Features

* DrnContext added
    * Implemented IDesignTimeDbContextFactory to enable migrations from dbContext defining projects.
    * Implemented IDesignTimeServices to support multi context projects with default output directory in the context specific folder.
    * Uses HasDrnContextServiceCollectionModule to automatic registration with AddServicesWithAttributes service collection extension method.
    * Uses context name (typeof(TContext).Name) as connection string key by convention.
    * Enables DRN.Framework.Testing to create easy and effective integration tests with conventions and automatic registrations.

### Bug Fixes

---
**Semper Progredi: Always Progressive**