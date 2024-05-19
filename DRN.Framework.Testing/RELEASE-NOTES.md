Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.4.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to 19 May Commemoration of Atatürk, Youth and Sports Day.

### New Features

*  LaunchExternalDependenciesAsync extension method added on WebApplicationBuilder to launch application all of its dependencies
  * DrnAppFeatures:LaunchExternalDependencies config should set true and Environment should be Development

## Version 0.3.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to 23 April ~~National Sovereignty and Children's Day~~.

### Breaking Changes

* ContainerContext refactored and each Postgres and RabbitMQ usages refactored into PostgresContext and RabbitMQContext.
* WebApplicationContext renamed as ApplicationContext

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

### Breaking Changes

### New Features

* TestContext added
* FactDebuggerOnly and TheoryDebuggerOnly test attributes added
* Following data attributes added:
  * DataInlineAutoAttribute
  * DataInlineContextAttribute
  * DataMemberAutoAttribute
  * DataMemberContextAttribute
  * DataSelfAutoAttribute
  * DataSelfContextAttribute
* SettingsProvider added.
* DataProvider added.

### Bug Fixes

---
**Semper Progredi: Always Progressive**