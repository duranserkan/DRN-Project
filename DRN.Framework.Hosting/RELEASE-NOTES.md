Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.  

## Version 0.5.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to August 30 Victory Day, a day that marks the decisive victory achieved by the Turkish people against imperialism during the Turkish War of Independence, leading to the establishment of the Republic of Türkiye.

### New Features

* ScopedUserMiddleware 
  * sets IScopedUser with current user belongs to the request scope
  * updates IScopedLog with UserId and UserAuthenticated info
* HttpScopeHandler
  * Initializes ScopeContext with TraceId, IScopedLog and IScopedUser
  * DrnException handling as default application exception handling
  * DrnExceptions can be used to short circuit the processing pipeline
  * FlurlHttpException handling as default gateway exception handling
  * In Development environment - HttpResponse returns ScopedLog as developer exception result
  * l5d-client-id is added to scoped log by default
* HttpRequestLogger
  * Request and response logs improvements
* DrnProgramBase 
  * HostOptions become configurable with Configuration.GetSection("HostOptions")
  * overrideable ConfigureSwaggerOptions
  * Added swagger support by default in development environment

### Breaking Changes

* DrnProgramBase
  * DrnProgramOptions - Removed

## Version 0.4.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to 19 May Commemoration of Atatürk, Youth and Sports Day.

### Breaking Changes

* HttpScopeLogger is renamed as HttpScopeHandler

### New Features

* EndpointsApiExplorer - added to service collection by DrnProgramBase to support OpenAPI Specification
* NexusClient - added for initial service discovery and remote configuration management development
* DrnProgramBase has new overridable configuration methods
  * ConfigureApplicationPreScopeStart
  * ConfigureApplicationPostScopeStart
  * MapApplicationEndpoints

## Version 0.3.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to 23 April National Sovereignty and Children's Day.

### New Features

* DrnProgramBase and IDrnProgram - added to minimize development efforts with sensible defaults
* HttpScopeLogger and HttpRequestLogger middlewares - added to support structured logging

---

**Semper Progredi: Always Progressive**