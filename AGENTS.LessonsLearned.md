# Lessons Learned

Record only durable, generalizable lessons. Do not keep one-time findings, incident history, or case details that cannot change future decisions across cases. If a case produces a durable rule, move the rule into the owning source (`AGENTS.md`, `.agent/rules/DiSCOS.md`, a skill, workflow, package doc, or source comment) and remove the case-specific lesson during cleanup.

When a lesson is still needed, remove the empty-state placeholder and append `## N. Descriptive Title` with `### Case`, `### General Rule`, `### Decision Boundary`, and `### Source To Update` subsections. Keep the case specific enough to recognize the failure mode, but make the rule portable enough to apply beyond that case.

## 1. Clarify Requires Answer And Develop Before Mutation

### Case
A workflow-doc improvement invoked `/clarify`, created a `CLARIFY-*` artifact, and then mutated the target workflow directly without first running `/answer` to produce a `DEVELOP-*` handoff and `/develop` to implement it.

### General Rule
When `/clarify` is used for work that will mutate files, the route must continue through `/answer`, a `DEVELOP-*` artifact, and `/develop`. Automatic execution may bridge stages only when autonomy and approval gates allow; manual execution waits for the user to invoke the next workflow, never direct mutation.

### Decision Boundary
This applies whenever `/clarify` is invoked for source, workflow, skill, documentation, or agent-control-plane changes. It does not require automatic progression for high-risk, unclear, security-sensitive, VCS, destructive, failed-gate, or user-blocked work; those cases stop at the owning gate until the next workflow is approved or manually invoked.

### Source To Update
Keep the invariant in `.agent/workflows/_shared/workflow-operating-model.md`, `.agent/workflows/clarify.md`, `.agent/workflows/answer.md`, and `.agent/workflows/develop.md` rather than relying on conversational memory.
