# Lessons Learned

Record only durable, generalizable lessons. Do not keep one-time findings, incident history, or case details that cannot change future decisions across cases. If a case produces a durable rule, move the rule into the owning source (`AGENTS.md`, `.agent/rules/DiSCOS.md`, a skill, workflow, package doc, or source comment) and remove the case-specific lesson during cleanup.

When a lesson is still needed, remove the empty-state placeholder and append `## N. Descriptive Title` with `### Case`, `### General Rule`, `### Decision Boundary`, and `### Source To Update` subsections. Keep the case specific enough to recognize the failure mode, but make the rule portable enough to apply beyond that case.

## 1. Recommend convention before configuration

### Case

A convention diagnostic recommended an alias mapping before explaining how to align the existing name or placement. This made an exception look like the normal fix and encouraged unnecessary configuration.

### General Rule

Recommend conventional fixes first. When a name and its namespace or folder disagree, show both valid alignments without assuming which represents the intended domain. Present explicit overrides last, only for intentional exceptions with a concrete reason.

### Decision Boundary

Preserve runtime behavior and persisted identities when choosing an alignment. A third-party contract or deliberate compatibility requirement may justify a narrow exception. Do not weaken the general rule or add an override merely to silence a diagnostic.

### Source To Update

Convention diagnostic messages and their contract tests own the actionable choices. The entity-framework skill owns the context-specific guidance. Keep both aligned when changing a convention.

## 2. Scope architecture rules before inferring ownership

### Case

A boundary analyzer treated navigation and utility namespaces as business ownership, forcing page models and helpers into unrelated source layouts.

### General Rule

Establish a type's architectural role before deriving ownership from its namespace. Navigation folders are not subdomains. Scope business boundaries to domain code and recognized repository, service, persistence and controller contracts. Test both included roles and nearby excluded types.

### Decision Boundary

Excluding presentation code from subdomain checks must not disable separate API exposure, dependency, authorization or data-classification rules. Nested implementation details retain the enclosing architectural boundary.

### Source To Update

The analyzer's shared scope predicate and convention reference own the scope. Framework skills should reference that contract rather than infer business roles from arbitrary folder names.
