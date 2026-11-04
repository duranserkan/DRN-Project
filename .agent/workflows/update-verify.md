---
description: Verification phase of /update — validate skill-body content against actual source code, staged per project family
---

> **Sub-workflow of `/update`**. Not invoked directly.
> Reads `Scope` and generates/resumes verification stages from executed stages.
> See also: [Status Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~2.2K tokens**

## 0. Progress File Contract

**Location**: `.agent/temp/update-verify-progress.md`.

If invoked directly, run the shared Startup Gate; otherwise inherit `/update` context.

Accept only plan status `reviewed` or `verifying`. A `failed` or `correcting` plan returns to `/update`, which delegates source corrections to `/update-execute`.

### Verification Binding

Before initializing or resuming:

1. Read the plan's Run ID, Requested Scope, effective Scope, and Semantic Plan SHA-256.
2. Require the reviewed transition and latest apply/correction approval for the same Run ID, requested/effective scope, and semantic plan. Otherwise return to the owning review/preview gate.
3. Resolve the exact bytewise-deduplicated Stage 1-5 `### Outputs` path set. Reject undeclared mutation, an in-scope mutating stage with `N/A`, absolute paths, or repository escapes.
4. Persist the output status and checksum artifacts defined by [baseline-inputs-hash-spec.md](./_shared/baseline-inputs-hash-spec.md). For an empty output set, use its explicit empty-artifact contract.
5. Set Output Revision SHA-256 based on output existence: hash `.agent/temp/update-verify-outputs.manifest` for non-empty outputs, and hash `.agent/temp/update-verify-output-status.z` (the zero-byte status artifact required by the shared empty-artifact contract) when outputs are empty. Record the selected artifact path in the progress record (`Output Revision Manifest`), and keep the SHA-256 value lowercase without using `N/A`.
6. Bind progress to Run ID, requested/effective scopes, Semantic Plan SHA-256, the selected output-revision artifact path, and Output Revision SHA-256.

Initialize missing progress from the template. Otherwise:

- A Run ID, requested/effective scope, or Semantic Plan SHA-256 mismatch supersedes progress; reinitialize every verification stage without reusing pass/skipped states.
- An Output Revision SHA-256 or exact selected-artifact mismatch resets every stage, including skipped stages, and Final to `pending`, clears checkpoints, and records the new binding before verification. Reevaluate skips under the new binding.
- Matching bindings resume at the first actionable stage:

- `pending` / `executing`: continue.
- `fail`: reset to `pending` only with matching correction progress `done` and plan `done -> reviewed`; otherwise return to `/update`.
- `blocked`: reset to `pending` only after its blocker resets or passes.
- `pass` / `skipped`: terminal only with a current complete binding.

```markdown
# Update Verification Progress
> Generated: <timestamp> | Run ID: <run-id> | Status: verifying | verified | failed
> Plan: .agent/temp/update-plan.md | Requested Scope: <scope> | Scope: <effective scope>
> Semantic Plan SHA-256: <sha256>
> Output Revision Manifest: <selected-output-revision-artifact-path> | Output Revision SHA-256: <sha256>

## Stage 0: Structural Integrity
> Status: pending | skipped | executing | pass | fail | blocked
### Checks & Findings

## Stage 1: Non-Project Asset Verification
> Status: pending | skipped | executing | pass | fail | blocked
### Checks & Findings

## Stages 2-N: Per-Project-Family Verification
> Status: pending | skipped | executing | pass | fail | blocked
> Projects: <list> | Last verified skill: <name>
### Checks & Findings

## Stage Final: Verdict
> Status: pending | executing | pass | fail | blocked
### Summary Table & Corrections Required Table
```

Rules:

- Generate one stage per project-family prefix; mark unaffected families `skipped`.
- Set plan status to `verifying` only after the complete verification binding is persisted.
- Verdict status:
  - `pass`: no `❌` errors; minor `⚠️` warnings allowed.
  - `fail`: any `❌` error or critical `⚠️`.
  - `skipped`: terminal and excluded from all-pass checks.
  - `blocked`: non-terminal; final verdict fails until cleared.
- Classify identifiers used to locate/load code as primary (`❌`): class, interface, method, attribute, config key, path.
- Classify illustrative snippets or example paths as secondary (`⚠️`).
- Checkpoint `Last verified skill` mid-stage for recovery.

---

## 1. Stage 0: Structural Integrity

Failure blocks all later stages; set them `blocked`.

- Skills/loaders: verify bidirectional included-directory/loader listings. Profile `Excluded Skills` must match baseline decisions plus currently approved changes; copying prior approval artifacts is unnecessary. Any loader, task-loading path, or index route referencing an excluded skill is `❌` and fails Stage 0. Omit excluded skills from union/content checks. Other missing directories/loader entries are `❌`.
- Missing profile extensions: enforce the [Stage 3 table contract](./update-execute.md#skill-discovery-and-custom-routes); exclude recorded missing skills from loader union validation and report in Stage 0.
- Custom loaders: verify each included `<custom>-*` prefix maps to `load-skills-<custom>.md`; uncategorized included skills map only to `load-skills-custom.md`.
- Union: verify included skills match `load-skills-all.md` in Standard Load Order with framework/profile conditions preserved; sort custom prefix loaders after Frontend and before generic custom.
- Workflow routes: verify `.agent/workflows/*.md` task routes match the `AGENTS.md` Workflows table and profile `Custom Workflow Routes`.
- Token estimates: compare summed skill sizes / 4 to `Estimated context:`. Flag delta >15% as `⚠️` only.
- Cross-references: verify against the plan drift report.
- Portability: resolve relative links from referring files. Reusable guidance must not require repository-local source/docs outside the copied bundle. Validate profile paths against the destination filesystem and external framework references against applicable versions. Report unavailable remote verification. Illustrative paths need not exist locally.
- References: resolve `AGENTS.md` project paths, profile custom load-set entries, and `overview-skill-index` skill directories.
- Bootstrap freshness (`all`): verify fresh filesystem discovery, persisted baseline artifacts, and pre-execution reproduction of Baseline Inputs Hash. Never recompute the baseline from post-execution outputs.

---

## 2. Stage 1: Non-Project Asset Verification

Verify non-project config references:

1. Confirm references exist.
2. Spot-check details such as framework versions and CI step names.
3. Flag stale references.

---

## 3. Stages 2-N: Per-Project Content Verification

For each project family, locate relevant skills by searching for `<FamilyPrefix>`.

Verify real application identifiers against local source. Check labeled example identifiers for internal consistency and adaptation guidance, not literal existence. For dependencies consumed as packages, use installed package APIs/docs or matching upstream source. Framework source projects need not exist in the destination repository. Record unavailable evidence as a verification gap, not as proof that a dependency type is missing.

1. Verify code identifiers:
   - Classes/interfaces exist.
   - Repository-local file paths exist. Upstream paths resolve in the referenced source repository and version.
   - Namespaces and `using` directives exist.
   - Methods/properties exist.
   - Config keys match `appsettings*.json` or source.
   - Custom attributes and DI lifetimes exist.
2. Verify pattern accuracy against representative code.

---

## 4. Final Verdict

Recompute the complete verification binding before aggregation. Any mismatch invalidates cached statuses under Section 0 and blocks a verdict.

Aggregate executed stages:

| Stage statuses | Verdict | Action |
|---|---|---|
| All non-skipped stages `pass`, no warnings | Verified | Plan status -> `verified` |
| All non-skipped stages `pass`, warnings present | Verified with warnings | List warnings; plan status -> `verified` |
| Any `fail` or `blocked` | Failed | Consolidate stable correction IDs/targets; plan status -> `failed` |
| Any `pending` or `executing` | Incomplete | Keep plan status -> `verifying` |

Success report:

```markdown
## Verification Complete
All non-skipped stages passed. Skill content is aligned.
Report the verified Run ID, requested/effective scopes, Semantic Plan SHA-256, and Output Revision SHA-256.
Warnings, if any, include evidence, impact, invariant, recommendation, confidence, and verification status in `.agent/temp/update-verify-progress.md`.
Next steps:
1. Review `.agent/temp/update-verify-progress.md` for warnings.
2. Delete `.agent/temp/update-plan.md` and `.agent/temp/update-verify-progress.md` only if cleanup was requested.
3. Commit only if explicitly requested: `git add AGENTS.md .agent/ && git commit -m "chore(skills): sync agent configuration"`
```

Failure report:

```markdown
## Verification Failed: Corrections Required
Drift detected. `/update-execute` owns source correction after `/update` produces and obtains approval for the exact correction preview.
| ID | Stage | Skill/File | Evidence | Impact | Invariant | Recommendation | Confidence | Verification |
|---|---|---|---|---|---|---|---|---|
```

---

## 5. Design Properties

- Source-read-only; lifecycle-mutating: verification does not edit repository source/configuration, but it writes the output path-state/manifest artifacts and updates `update-plan.md` and `update-verify-progress.md` statuses/findings. Cleanup requires request.
- Revision-bound: no pass, skip, or final verdict survives a Run ID, scope, semantic-plan, output presence, supported type, path-set, or byte change. Canonical mode remains apply/resume safety evidence rather than Output Revision identity.
- Fail-fast: Stage 0 blocks downstream checks.
- Context-safe: checkpoint mid-stage for multi-window execution.
