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

## 2. Check CLI Paths and Child Process Arguments

### Case
A report reader accepted a path outside its package. A test runner forwarded positional arguments to a child process, where a filename such as `--version` could select an option and skip the tests.

### General Rule
For each new or changed CLI argument, trace its value to the file operation or child process that consumes it. When a command promises to stay inside a directory, verify the resolved destination before reading or writing. Account for symlinked parents and existing output files. When forwarding filenames, select permitted operands and pass them after the child's own `--` if it supports one. A shell-free argument array still allows the child to parse options.

### Decision Boundary
Use the command's documented scope to decide which paths and options are valid. Check a valid input alongside counterexamples such as `../outside.info`, an external absolute path, a symlink leading outside the allowed directory, and a filename beginning with `-`. Verify that rejected inputs cause no unintended file access and cannot turn a skipped operation into a successful exit.

### Source To Update
Carry these checks into the security pass in [the review workflow](.agent/workflows/review.md) during lessons cleanup.

## 3. Validate Redirect Destinations

### Case
A sign-in flow accepts a return URL before authentication and redirects after a later request. A caller can submit a different destination at that later step.

### General Rule
Trace untrusted return URLs through forms, query strings, and stored state to the final redirect or browser navigation. For an in-app return, use the framework's local-URL check and local redirect API. Fall back to a known local route when validation fails. If external redirects are required, parse the URL and compare its origin, including scheme and port, against the explicitly permitted destinations.

### Decision Boundary
Apply this check when input can influence the destination. Preserve valid local paths and their query strings. Check an absolute external URL, a scheme-relative URL such as `//outside.example`, and backslash or encoded forms as interpreted by the actual framework or browser. For external allowlists, also check lookalike hosts and user-info URLs.

### Source To Update
Carry this rule into the [security checklist](.agent/skills/basic-security-checklist/SKILL.md) during lessons cleanup.
