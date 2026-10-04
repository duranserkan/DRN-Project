# Lessons Learned

Record only durable, generalizable lessons. Do not keep one-time findings, incident history, or case details that cannot change future decisions across cases. If a case produces a durable rule, move the rule into the owning source (`AGENTS.md`, `.agent/rules/DiSCOS.md`, a skill, workflow, package doc, or source comment) and remove the case-specific lesson during cleanup.

When a lesson is still needed, remove the empty-state placeholder and append `## N. Descriptive Title` with `### Case`, `### General Rule`, `### Decision Boundary`, and `### Source To Update` subsections. Keep the case specific enough to recognize the failure mode, but make the rule portable enough to apply beyond that case.

## 1. Document Contracts, Not Routine Repairs

### Case
A bug fix prompted repeated descriptions of already-expected behavior across package docs, skills, and the repository profile.

### General Rule
Keep documentation edits minimal, natural, and surgical. Do not add bug fixes to agent files when they only restore expected behavior. Record the repair in code and regression tests.

### Decision Boundary
Update docs when a public contract, usage requirement, or durable convention changes. Keep details in the owning document and link to it elsewhere. Follow repository release-note rules separately.

### Source To Update
Consolidate this rule into `basic-documentation` and the repository's documentation-sync guidance during lessons cleanup.
