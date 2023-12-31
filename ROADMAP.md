# Roadmap
- [X] DRN.Framework (0.2.0 released)
- [X] Management best practices 
- [X] Engineering manifest
- [X] Reference documents for design, architecture and microservices
- [ ] DRN.Nexus  (will be released with 1.0.0)
## DRN.Framework
### DRN.Framework.Testing
- [X] TestContext
  - MethodContext
  - ContainerContext
  - WebApplicationContext
- [X] DataAttributes
  - DataInlineAttribute
  - DataMemberAttribute
  - DataSelfAttribute
- [X] Debugger only test attributes
  - FactDebuggerOnly
  - TheoryDebuggerOnly
- [X] DataProvider & TestContext conventions
- [X] Settings Provider & TestContext conventions
### DRN.Framework.Utils
- [X] DependencyInjection
  - ScopedAttribute, ScopedWithKeyAttribute
  - TransientAttribute, TransientWithKeyAttribute
  - SingletonAttribute, SingletonWithKeyAttribute
  - HasServiceCollectionModuleAttribute
  - Service collection registration & Service provider validation extensions
- [X] AppSettings
- [X] Configuration
  - JsonSerializerConfigurationSource
- [ ] Scoped Logs
### DRN.Framework.SharedKernel
- [X] Domain Entities and definitions
- [X] Generalized Convention Exceptions
- [X] AppConstants
  - Application specific TempPath 
  - ApplicationId
  - Local IP address
- [ ] Source Known Ids
### DRN.Framework.EntityFramework
- [X] DDD style entities, aggregates
- [X] DrnContext inherited from DbContext and its DbContext conventions for rapid domain design and development
- [ ] Postgresql based remote dictionary with retentions
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
### DRN.Framework.Asp
- [ ] Secure endpoints
- [ ] Scoped log support
- [ ] Endpoint topology
### DRN.Framework.Hosting
- [ ] Service discovery
- [ ] Remote settings
- [ ] Self documenting microservices with unified topology
## DRN.Nexus
- [ ] Service discovery
- [ ] Remote settings
- [ ] Self documenting microservices with unified topology