Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.  

## Version 0.5.0

### New Features
* HttpScopeHandler
  * DrnException handling added as default application exception handling
  * DrnExceptions can be used to short circuit the processing pipeline
  * FlurlHttpException handling added as default gateway exception handling
  * In Development environment - HttpResponse returns ScopedLog as developer exception result
  * l5d-client-id is added to scoped log by default
* HttpRequestLogger
  * Request and response logs improved
* DrnProgramBase - made HostOptions configurable with Configuration.GetSection("HostOptions")

### Breaking Changes

### Bug Fixes

## Version 0.4.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to 19 May Commemoration of Atatürk, Youth and Sports Day.

### Breaking Changes

* HttpScopeLogger is renamed as HttpScopeHandler

### New Features

* EndpointsApiExplorer added to service collection by DrnProgramBase to support OpenAPI Specification
* NexusClient added for initial service discovery and remote configuration management development
* DrnProgramBase has new overridable configuration methods
  * ConfigureApplicationPreScopeStart
  * ConfigureApplicationPostScopeStart
  * MapApplicationEndpoints

## Version 0.3.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to 23 April National Sovereignty and Children's Day.

### Breaking Changes

### New Features

* DrnProgramBase and IDrnProgram added to minimize development efforts with sensible defaults
* HttpScopeLogger and HttpRequestLogger middlewares added to support structured logging

### Bug Fixes

---
**Semper Progredi: Always Progressive**