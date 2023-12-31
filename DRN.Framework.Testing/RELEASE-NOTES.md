Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

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