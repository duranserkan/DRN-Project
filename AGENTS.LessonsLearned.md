# Lessons Learned

When a durable lesson is discovered, remove the empty-state placeholder and append a concise numbered entry in `## N. Descriptive Title` format only if it is not already covered by `AGENTS.md`, `.agent/rules/DiSCOS.md`, an existing skill, a workflow, package documentation, or source comments. Prefer general rules over one-time incident history, and move stable guidance into the owning skill or workflow during the next documentation sync.

## 1. Tag-Triggered Sonar Gates Need Branch Context

### Context

Release and prerelease workflows run from `refs/tags/...`, but SonarCloud quality gates and reports are branch-oriented.

### Preferred Approach

Before publishing from a release tag, verify the tag commit equals the protected source branch HEAD. Run the Sonar gate with explicit branch parameters for that source branch, then publish only after the branch-attached quality gate passes.

### Rule

Do not rely on tag ref context for SonarCloud release gating; attach release analysis to the protected branch being released.

## 2. Docker Digest Scans Can Still Use Cleanup Tags

### Context

Buildx rejects a build-push step that combines a named tag with `push-by-digest=true`, but release pipelines still need deterministic staged tags to clean up failed runs. A custom image exporter with an untagged repository name can also publish `latest` before the scan and promotion gates run.

### Preferred Approach

Set a run-scoped `staged-*` tag, use `push: true`, validate the emitted `sha256` digest, scan `registry://repo@sha256:<digest>`, and promote only the scanned digest.

### Rule

Do not use `push-by-digest=true` or a custom untagged image exporter in the same build step that must create a cleanup tag.
