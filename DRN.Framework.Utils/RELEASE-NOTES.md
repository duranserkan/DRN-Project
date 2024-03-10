Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.  

## Version 0.2.0

### New Features

* HasServiceCollectionModuleAttribute has PostStartupValidationAsync when,
  * ValidateServicesAddedByAttributes extension method called from service provider,
  * PostStartupValidationAsync will be called if all services resolved successfully.
  * For instance, DrnContext can apply migrations after service provider services resolved successfully.

## Version 0.2.0

### Breaking Changes

* LifetimeContainer renamed as DrnServiceContainer
* Lifetime attributes moved to DRN.Framework.Utils.DependencyInjection.Attributes namespace

### New Features

* JsonSerializerConfigurationSource added to add dotnet objects to configuration
* RemoteJsonConfigurationSource added to remote settings to configuration (experimental)
* ConnectionStringsCollection added as poco model to serialize connection strings
* StringExtensions added
  * ToStream method added to convert strings to in memory stream
* HasServiceCollectionModuleAttribute added

## Version 0.1.0

### Breaking Changes

### New Features

* AppSettings added
* ServiceCollectionExtensions added
  * ReplaceInstance
  * ReplaceTransient
  * ReplaceScoped
  * ReplaceSingleton
* Attribute based dependency injection added
  * ScopedAttribute, TransientAttribute, SingletonAttribute and LifetimeAttribute added
  * ScopedWithKeyAttribute, TransientWithKeyAttribute, SingletonWithKeyAttribute and LifetimeWithKeyAttribute added
  * ServiceCollection AddServicesWithAttributes extension added
  * ServiceProvider ValidateServicesAddedByAttributes extension added

### Bug Fixes