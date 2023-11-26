# Roadmap
- [X] DRN.Framework (0.1.0 released)
- [X] Management best practices 
- [X] Engineering manifest
- [X] Reference documents for design, architecture and microservices
- [ ] DRN.Nexus  (will be released with 1.0.0)
## DRN.Framework
### DRN.Framework.Testing
- [X] TestContext 
- [X] DataAttributes
  - DataInlineAutoAttribute & DataInlineContextAttribute
  - DataMemberAutoAttribute & DataMemberContextAttribute
  - DataSelfAutoAttribute & DataSelfContextAttribute
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
  - Service collection registrations & Service provider validations
- [X] AppSettings
- [ ] Scoped Logs
### DRN.Framework.SharedKernel
- [X] Domain Exceptions
- [X] AppConstants
  - Application specific TempPath 
  - ApplicationId
  - Local IP address
- [ ] Source Known Ids
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
### DRN.Framework.EntityFramework
- [ ] Postgresql based remote dictionary with retentions
- [ ] Migration manager that supports sharding and schema based multi tenancy
- [ ] DDD style entities, aggregates, repositories
### DRN.Framework.Hosting
- [ ] Service discovery
- [ ] Remote settings
- [ ] Self documenting microservices with unified topology
## DRN.Nexus
- [ ] Service discovery
- [ ] Remote settings
- [ ] Self documenting microservices with unified topology