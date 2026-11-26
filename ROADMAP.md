# Roadmap
- [X] DRN.Framework (0.6.0 released)
- [X] Management best practices
- [X] Engineering manifest
- [X] Reference documents for design, architecture and microservices
- [ ] DRN.Nexus  (will be released with 1.0.0)
- [ ] Complete the [Privacy Checklist](docs/PRIVACY-CHECKLIST.md) evaluations for diagnostics and future DbContext support.
- [ ] Address the [SOC2 Checklist](docs/SOC2-CHECKLIST.md) gaps for SOC 2 preparedness.

### DRN.Framework.Testing
- [X] DrnTestContext
  - MethodContext
  - ContainerContext
    - PostgresContext
    - RabbitMQContext
  - ApplicationContext
- [X] DataAttributes
  - DataInlineAttribute
  - DataMemberAttribute
  - DataSelfAttribute
- [X] Debugger only test attributes
  - FactDebuggerOnly
  - TheoryDebuggerOnly
- [X] DataProvider & DrnTestContext conventions
- [X] Settings Provider & DrnTestContext conventions
- [ ] AuthContext for OpenID Connect (OIDC)
- [ ] EmailContext with MailHog
- [ ] MessageBrokerContext with MassTransit

### DRN.Framework.Utils
- [X] DependencyInjection
  - ScopedAttribute, ScopedWithKeyAttribute
  - TransientAttribute, TransientWithKeyAttribute
  - SingletonAttribute, SingletonWithKeyAttribute
  - HasServiceCollectionModuleAttribute
  - Service collection registration & Service provider validation extensions
- [X] AppSettings
  - ConfigurationDebugView
- [X] Configuration
  - JsonSerializerConfigurationSource
- [X] Scoped Logs

### DRN.Framework.SharedKernel
- [ ] Explore additional progressive domain-safety cases and activation conditions beyond the required layer references. Validate source evidence, false positives and intentional exceptions before adding rules.
- [ ] Extend `DRNDTO003` to preserve `[Personal]` and `[Sensitive]` on explicit property mappings between application-owned models, including request-to-entity and entity-to-view-model mappings. Cover object initializers, assignments after construction, and copies through a local assigned once. Warn when the source and destination properties are identifiable and the destination lacks a required classification.
  - When another attribute rule needs to trace a source property to its destination, extract and reuse that mapping logic. Each rule defines its own attribute requirements and diagnostics.
  - For `profile.ContactEmail = request.Email`, the extended `DRNDTO003` would warn if `Email` has `[Personal]` and `ContactEmail` lacks it. A separate length rule could use the same mapping to flag a direct copy from `[MaxLength(254)]` to `[MaxLength(100)]` because a valid source value could be too long for the destination.
- [X] Domain Entities and definitions
- [X] Generalized Convention Exceptions
- [X] AppConstants
  - Application specific TempPath
  - ApplicationId
  - Local IP address
- [X] Source Known Ids

### DRN.Framework.EntityFramework
- [X] DDD style entities, aggregates
- [X] DrnContext inherited from DbContext and its DbContext conventions for rapid domain design and development
- [ ] Domain events with transactional outbox
- [ ] PostgreSQL based remote dictionary with retentions
- [ ] Migration manager that supports sharding and schema based multi tenancy

### DRN.Framework.Masstransit
- [ ] Convention & Settings based consumer, batch consumer, request consumer registrations
- [ ] Scoped log support
- [ ] Second level retry
- [ ] Timeouts with consumer context cancellation tokens
- [ ] Exception type based error queues
- [ ] Observable and Traceable Messages
- [ ] Consumer Topology

### DRN.Framework.Jobs
- [ ] Cron based recurring jobs
- [ ] Scheduled jobs
- [ ] Active passive distributed jobs
- [ ] Job Topology

### DRN.Framework.Hosting
- [X] Http request scoped log support
- [ ] Secure endpoints
- [ ] Endpoint topology
- [ ] Service discovery
- [ ] Remote settings
- [ ] Self-documenting microservices with unified topology

## DRN.Nexus
- [ ] Service discovery
- [ ] Remote settings
- [ ] Self-documenting microservices with unified topology
