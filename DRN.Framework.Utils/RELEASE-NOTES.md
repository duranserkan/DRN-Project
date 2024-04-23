Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.  

## Version 0.3.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to 23 April National Sovereignty and Children's Day.

### New Features
* AppSettings now has GetDebugView() method that returns ConfigurationDebugView
  * ConfigurationDebugView has ToSummary() method that returns human friendly configuration summary model.
* AppSettings now has GetValue<> and Get<> methods to get values from configuration
* MountedSettingsConventions added.
  * /appconfig/json-settings json files will be added to configuration if any exist
  * /appconfig/key-per-file-settings files will be added to configuration if any exist
  * IMountedSettingsConventionsOverride overrides default /appconfig location if added to service collection before host built
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

---
**Semper Progredi: Always Progressive**