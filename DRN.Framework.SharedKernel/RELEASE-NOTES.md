Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version 0.3.0

My family celebrates the enduring legacy of Mustafa Kemal Atatürk's enlightenment ideals. This release is dedicated to 23 April Turkish National Sovereignty and Children's Day.

Semper Progredi: Always Progressive

### Breaking Changes

* JsonSerializerOptions moved to JsonConventions. System.Text.Json defaults will be overridden by JsonConventions when
  * TestContext is used in tests
  * DrnHostBuilder is used to build host

### New Features

* Entity and AggregateRoot base classes' ModifiedAt property now has `ConcurrencyCheck` attribute and can be used for optimistic concurrency.

### Bug Fixes

## Version 0.2.0

### New Features

* JsonSerializerOptions added to AppConstants which is same with default dotnet settings for now.
* AggregateRoot, Entity, DomainEvent definitions added

## Version 0.1.0

### Breaking Changes

### New Features

* AppConstants added
* DRN Framework exceptions added
  * ValidationException
  * NotFoundException
  * NotSavedException
  * ExpiredException
  * ConfigurationException

### Bug Fixes