# Privacy and compliance

Evaluation backlog for diagnostic privacy and future DbContext support. Compare the current solution with Microsoft-maintained packages and widely adopted open-source alternatives. Apply the Priority Stack: security, correctness, clarity, simplicity, then measured performance. Prefer small integrations with clear value and minimal dependency cost.

- [ ] Evaluate [Microsoft .NET data redaction](https://learn.microsoft.com/en-us/dotnet/core/extensions/data-redaction?tabs=dotnet-cli) for redactor interoperability. Compare protection, failure behavior, capture limits, and performance with the current diagnostic policy.
- [ ] Evaluate [Microsoft .NET data classification](https://learn.microsoft.com/en-us/dotnet/core/extensions/data-classification?tabs=dotnet-cli) as a shared classification contract. Check support for selected library attributes and reuse of DRN attributes across logging and DbContext integrations.
- [ ] Evaluate [Microsoft .NET audit reports](https://learn.microsoft.com/en-us/dotnet/core/extensions/audit-reports?tabs=dotnet-cli) for build-time visibility into sensitive telemetry. Check coverage of the current logging APIs and classification attributes, report usefulness, and CI cost.
- [ ] Evaluate [ASP.NET Core HTTP logging redaction](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-logging/?view=aspnetcore-10.0#redacting-sensitive-data) for integration with scoped logs. Check capture limits, correlation, duplicate logging, security, and performance.
- [ ] Evaluate EF Core native [logging](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/extensions-logging) and [SaveChanges interception](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors#example-savechanges-interception-for-auditing) for DbContext diagnostics and change auditing. Compare ChangeTracker-based capture with existing libraries, including attribute compatibility, transaction behavior, sensitive-value protection, and overhead.

Current behavior: [Diagnostic privacy and linked logs](../DRN.Framework.Utils/Logging/Diagnostics/README.md).
