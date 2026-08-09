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

## 2. Cancellation Aggregators Need Stable Effective Tokens

### Case

A cancellation utility merged a new external token by replacing its linked `CancellationTokenSource` and disposing the previous source. Repeated merges could unregister earlier external tokens, while tokens already handed to asynchronous operations no longer observed later merges or manual cancellation. Adding named child scopes exposed two more failure modes: one global child coupled unrelated component groups, and normal terminal cancellation retained now-useless external registrations until parent disposal.

### General Rule

Keep one stable effective token per cancellation scope when requests arrive over time. Use stable typed keys for component or workflow groups, and caller-owned linked token sources for instance-specific or operation-specific isolation. Detach merged registrations as soon as terminal cancellation completes. Mark the parent disposed and snapshot/detach named children under its lock, but cancel and dispose them only after releasing it. If disposal is requested reentrantly from a root callback, defer child and root cleanup until root cancellation completes so downward propagation cannot be unregistered midway.

### Decision Boundary

This applies to scoped or long-lived cancellation aggregators that support incremental merging, manual cancellation after operations start, or parent-owned named children. Named child scopes are not caller-owned and must never be used as dynamic per-instance or per-operation storage; link local tokens with a caller-owned source instead. A single linked source remains appropriate when every input token is known at construction time, the effective token is never replaced, and the owner disposes the source only after all consumers finish.

### Source To Update

Keep the invariant in the owning cancellation utility, parent/child and reentrant-disposal concurrency tests, and consumer documentation that distinguishes root, named-group, and local-operation cancellation.

## 3. High-Signal Package Release Notes

### Case

During framework package refactoring, release notes included low-value entries such as internal XML comment clarifications and duplicated descriptions of breaking API changes across both `Breaking Changes` and `Changed` sections.

### General Rule

Keep package release notes high-signal, concise, and non-redundant. Document consumer-visible behavior changes, breaking API changes, security fixes, new public features, configuration/default changes, operational behavior changes, data or migration changes, observable bug fixes, published artifacts, and relevant dependency, runtime, or container changes. Omit pure documentation/comment edits, non-functional refactorings, and redundant explanations that duplicate entries in other sections of the same release notes.

### Decision Boundary

Applies to all module and framework package `RELEASE-NOTES.md` files. Package READMEs, architecture docs, or skills should be updated when patterns change, but release notes must remain strictly focused on actionable, non-duplicative information for downstream package consumers.

### Source To Update

Keep the invariant in `AGENTS.md` (Release Notes discovery/rules), `basic-documentation` skill, and package release notes across the repository.

## 4. Native HTML Elements for Interactive UI Accessibility (SonarQube S6848)

### Case

Non-interactive HTML elements (`<div>`, `<span>`) assigned `onClick` event handlers were flagged as SonarQube accessibility code smells (`S6848` / `jsx-a11y/no-static-element-interactions`). Replacing them with native `<button>` elements fixed the accessibility finding, but converting `<div>` to `<button>` required explicit `type="button"` attributes to prevent default form submission side-effects, plus CSS styling (`text-left`, `w-full`, `font-family: inherit`) to preserve visual alignment and width parity.

### General Rule

Prefer native interactive HTML elements (`<button type="button">`, `<a>`) over non-native elements (`<div onClick=...>`, `<span onClick=...>`) for interactive UI controls. When converting non-native elements to `<button>`, always explicitly set `type="button"` (to avoid defaulting to `type="submit"` inside forms) and ensure CSS resets or utility rules handle element-specific defaults (`text-align`, `width`, `font-family`). Note that React/JSX `onClick={fn}` handlers compile to programmatic `addEventListener` calls inside external script bundles and remain fully CSP-compliant without requiring inline script permissions.

### Decision Boundary

Applies to all frontend components (React TSX, Razor partials, HTMX elements, and vanilla JS UI components) where click or tap handlers trigger client-side actions. When a non-native element cannot be replaced with a native `<button>` or `<a>` (e.g. specialized custom drag-and-drop handles), explicitly add ARIA roles (`role="button"`), keyboard navigation (`tabIndex={0}`, `onKeyDown`/`onKeyUp`), and focus indicators.

### Source To Update

Keep the invariant in `basic-security-checklist`, `modern-web-guidance`, `drn-buildwww-react`, and frontend UI development skills across the repository.
- TODO: Create dedicated accessibility skill or knowledge item in agent files (e.g. `.agent/skills/basic-accessibility/` or knowledge artifact) covering WCAG compliance, SonarQube accessibility rules (S6848/S6847), ARIA role patterns, and native HTML interactive element standards.
