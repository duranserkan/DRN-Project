Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.5.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to August 30 Victory Day, a day that marks the decisive victory achieved by the Turkish people against imperialism during the Turkish War of Independence, leading to the establishment of the Republic of Türkiye.

### Breaking Changes

* DrnException implementations - refactored
  * Http status code parameter added
  * ExceptionFor factory class added to create DrnExceptions as needed

## Version 0.4.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to 19 May Commemoration of Atatürk, Youth and Sports Day.

## Version 0.3.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to 23 April National Sovereignty and Children's Day.

### Breaking Changes

* JsonSerializerOptions - moved to JsonConventions. System.Text.Json defaults will be overridden by JsonConventions when
  * TestContext is used in tests
  * DrnHostBuilder is used to build host

### New Features

* Entity and AggregateRoot base classes' ModifiedAt property now has `ConcurrencyCheck` attribute and can be used for optimistic concurrency.

### Bug Fixes

* `AppConstants` LocalIpAddress calculation exception handling

## Version 0.2.0

### New Features

* JsonSerializerOptions - added to AppConstants which is same with default dotnet settings for now.
* AggregateRoot, Entity, DomainEvent

## Version 0.1.0

### New Features

* AppConstants 
* DRN Framework exceptions
  * ValidationException
  * NotFoundException
  * NotSavedException
  * ExpiredException
  * ConfigurationException

---
**Semper Progredi: Always Progressive**