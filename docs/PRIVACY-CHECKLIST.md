# Privacy Checklist

Evaluation checklist for diagnostic privacy and future DbContext support. Items below track candidate integrations, not implemented capabilities or completed compliance controls.

## Scope and current behavior

The [diagnostic privacy README](../DRN.Framework.Utils/Logging/Diagnostics/README.md) owns implemented behavior, configuration, and limitations. Use it as the baseline for each evaluation.

This checklist covers classification, redaction, telemetry inspection, HTTP logging, and potential DbContext integration. Broader organizational controls belong in the [SOC2 preparedness checklist](SOC2-CHECKLIST.md).

## Evaluation approach

Compare the current solution with Microsoft-maintained packages and widely adopted open-source alternatives. Apply the Priority Stack in order: security, correctness, clarity, simplicity, then measured performance. Prefer small integrations with clear value and minimal dependency cost.

Mark an item complete when its evaluation records a decision and supporting evidence. Completion does not require adoption. Record implementation work separately when an integration is selected.

## Integration checklist

### Classification and redaction

- [ ] **Data classification:** Evaluate [Microsoft .NET data classification](https://learn.microsoft.com/en-us/dotnet/core/extensions/data-classification?tabs=dotnet-cli) as a shared classification contract. Check support for selected library attributes and reuse of DRN attributes across logging and DbContext integrations.
- [ ] **Data redaction:** Evaluate [Microsoft .NET data redaction](https://learn.microsoft.com/en-us/dotnet/core/extensions/data-redaction?tabs=dotnet-cli) for redactor interoperability. Compare protection, failure behavior, capture limits, and performance with the current diagnostic policy.

### Telemetry inspection and HTTP logging

- [ ] **Audit reports:** Evaluate [Microsoft .NET audit reports](https://learn.microsoft.com/en-us/dotnet/core/extensions/audit-reports?tabs=dotnet-cli) for build-time visibility into sensitive telemetry. Check coverage of the current logging APIs and classification attributes, report usefulness, and CI cost.
- [ ] **HTTP logging:** Evaluate [ASP.NET Core HTTP logging redaction](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-logging/?view=aspnetcore-10.0#redacting-sensitive-data) for integration with scoped logs. Check capture limits, correlation, duplicate logging, security, and performance.

### Future DbContext support

- [ ] **DbContext diagnostics and change auditing:** Evaluate EF Core native [logging](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/extensions-logging) and [SaveChanges interception](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors#example-savechanges-interception-for-auditing). Compare ChangeTracker-based capture with existing libraries, including attribute compatibility, transaction behavior, sensitive-value protection, and overhead.

## Evaluation record

For each completed evaluation, record:

- Candidate and version evaluated, with links to source documentation.
- Comparison with current DRN behavior, including gaps and failure cases.
- Security and correctness evidence, plus measured performance when relevant.
- Dependency and maintenance cost.
- Decision: adopt, defer, or reject, with rationale.
- Follow-up work and acceptance criteria if adoption is selected.
