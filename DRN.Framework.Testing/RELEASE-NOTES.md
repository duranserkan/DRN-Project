Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.5.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to August 30 Victory Day, a day that marks the decisive victory achieved by the Turkish people against imperialism during the Turkish War of Independence, leading to the establishment of the Republic of Türkiye.

### New Features
* ITestStartupJob interface - added to run startup tasks before any TestContext is created.
  * PostgresContainerSettings or RabbitMQContainerSettings can be updated in a job that implements ITestStartupJob in the test project
* TestContext
  * FlurlHttpTest property to mock http requests
  * GetSettingsData
  * GetSettingsPath
* ContainerContext
  * BindExternalDependenciesAsync
  * PostgresContext
    * static PostgresContainerSettings property - added to provide PostgresContext defaults 
  * RabbitMQContext
    * static RabbitMQContainerSettings property - added to provide RabbitMQContext defaults
* ApplicationContext
  * CreateApplicationAndBindDependencies - added with Most used defaults and bindings
  * CreateClientAsync - added with most used defaults and bindings
  * GetCreatedApplication - added to get already application
  * LogToTestOutput - added to get application logs with ITestOutputHelper
* DataProvider - added GetDataPath 
* SettingsProvider - added GetSettingsPath and GetSettingsData

### Breaking Changes

* ContainerContext
  * PostgresContext
    * BuildContainer parameters are refactored into PostgresContainerSettings with Image Tag and Version settings
  * RabbitMQContext
    * BuildContainer parameters are refactored into RabbitMQContainerSettings with Image Tag and Version settings
* DataProvider
  * Get - returns DataProviderResult instead of string value
* LaunchExternalDependenciesAsync - IScopedLog and IAppsettings parameters refactored

## Version 0.4.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to 19 May Commemoration of Atatürk, Youth and Sports Day.

### New Features

* LaunchExternalDependenciesAsync extension method is added on WebApplicationBuilder to launch application all of its dependencies
  * DrnAppFeatures:LaunchExternalDependencies config should set true and Environment should be Development
* ApplicationContext.LogToTestOutput method added to configure TestOutput as serilog sink

## Version 0.3.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to 23 April ~~National Sovereignty and Children's Day~~.

### Breaking Changes

* ContainerContext - refactored and each Postgres and RabbitMQ usages refactored into PostgresContext and RabbitMQContext.
* WebApplicationContext - renamed as ApplicationContext

### New Features

* PostgresContext and RabbitMQContext's now have global instances and isolated contexts
* ApplicationContext's LogToTestOutput method redirects application logs to test output when ITestOutputHelper is provided

### Bug Fixes

* Postgres container doesn't respect custom options

## Version 0.2.0

### Breaking Changes

* Data context and auto attributes unified into:
  * DataInlineAttribute
  * DataMemberAttribute
  * DataSelfAttribute
* Old data attributes removed.

### New Features

* TestContext exposes AddToConfiguration to add poco objects to configuration root with System.Text.Json.
* TestContext exposes BuildConfigurationRoot method.
* TestContext exposes GetConfigurationDebugView method.
* TestContext exposes ContainerContext and WebApplicationContext.
* FactDebuggerOnly and TheoryDebuggerOnly test attributes
* Following data attributes added:
  * DataInlineAttribute
  * DataMemberAttribute
  * DataSelfAttribute
* If TestContext is first parameter of the test method, data attributes will automatically detect and provide it.

## Version 0.1.0

### New Features

* TestContext 
* FactDebuggerOnly and TheoryDebuggerOnly test attributes
* Following data attributes added:
  * DataInlineAutoAttribute
  * DataInlineContextAttribute
  * DataMemberAutoAttribute
  * DataMemberContextAttribute
  * DataSelfAutoAttribute
  * DataSelfContextAttribute
* SettingsProvider
* DataProvider

---
**Semper Progredi: Always Progressive**