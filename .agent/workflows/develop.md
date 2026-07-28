---
description: Implement approved DEVELOP artifacts with repository guidance
---

> **Pipeline**: `/clarify` -> `/answer` -> `/develop` (3/3) · [Lifecycle](./_shared/status-lifecycle.md) · [Operating Model](./_shared/workflow-operating-model.md)
> **Estimated context: ~1.5K tokens**

## 1. Resolve And Validate

Act concurrently as software-construction/design engineer grounded in *Code Complete, 2nd Edition*, PMP-caliber project/program leader, and forward-deployed engineer.

Run the Startup Gate once and load only needed skills.

| Input | Action |
|---|---|
| Explicit canonical `DEVELOP-*` | Use it. |
| Explicit `CLARIFY-*` | Route through `/answer` Section 5, then rerun. |
| None | Select one DEVELOP artifact; ask on multiple; route through CAD on none. |
| Inline/generic plan | Stop; require current `DEVELOP-*`. |

Never mutate source without a valid DEVELOP handoff.

| Status | Action |
|---|---|
| `ready-to-develop` | Validate and continue. |
| `implementing` | Revalidate and resume. |
| `implemented-pending-approval` | Verify final state; wait for user approval. |
| `clarified` | Run `/answer` handoff generation. |
| `draft-self-reviewed` | Apply the skip gate below; never implement directly. |
| `draft`, `clarifying`, missing | Return to `/clarify` and `/answer`. |
| `implemented` | Resume only on explicit confirmation. |

### CLARIFY Skip Gate

This gate authorizes only `/answer` handoff generation. Require testable criteria, unambiguous scope, addressed security, no unverified assumption, mitigated accepted assumptions, and explicit or valid workflow-tolerated approval.

### Freshness, Review, Approval

Verify DEVELOP `source`, `source_status`, `source_updated`, and `source_sha256` against the current non-superseded CLARIFY artifact.

- Missing/newer/mismatched/superseded source: set/report `stale: true`; regenerate through `/answer`.
- `needs_review: true`: run `/review`; clear only with no unresolved Critical or Major findings.
- `approval_required: true`: obtain explicit approval unless the shared contract permits a workflow-tolerated record.
- Direct `/develop <path>` may approve the reviewed artifact, persisted preview/diff when applicable, bounded scope, and risk. Compute and record the complete shared approval envelope before edits.
- A false approval flag without a current matching envelope blocks.

Security-sensitive, destructive, VCS, failed, unclear, or non-tolerable gates always require explicit human approval. Set `status: implementing` only after all gates pass and before source mutation.

### Completeness

Require:

- Current source metadata; no stale, review, approval, `blocked_on_user: true`, or unverified-assumption blocker.
- Clear scope, requirements, PBIs, dependencies, and testable criteria.
- Implementation Context with files, skills, command authorization, and static verification.
- Lineage Notes when continuing prior work.
- Lens/tradeoff traceability in criteria, constraints, risks, or Priority Stack.

A handoff with `blocked_on_user: true` cannot advance to `implementing`: route a fully scoped unresolved decision to explicit approval, or return incomplete scope and critical decision gaps to `/clarify`. Route other resolvable handoff defects to `/answer`.

## 2. Context And Plan

Read the operating model, skill index, `basic-agentic-development`, and only the handoff sections and skills needed by selected PBIs.

| Need | Load |
|---|---|
| Domain/entity | DDD overview plus profile domain skills |
| API/hosting | Security, API testing, profile hosting |
| Frontend | Matching frontend subset |
| Testing | Testing profile plus relevant test skills |
| Infrastructure | Repository structure and GitHub Actions |
| Docs | Documentation and diagram skills |

With no filter, implement the full backlog; otherwise include dependencies and warn on omissions.

For each PBI:

1. Map files, tasks, risks, constraints, and dependencies.
2. Classify Trivial, Standard, Significant, or Critical.
3. Stop on unverified assumptions.
4. Resolve conflicts with TRIZ, then Priority Stack.
5. Preserve approved strategy unless evidence proves it stale, contradictory, unsafe, or impossible.

Proceed after a concise summary for Trivial/Standard. Significant requires explicit or accepted workflow-tolerated approval; Critical/security-sensitive requires explicit approval. Track PBIs when count >=3.

VCS preflight: inspect branch, dirty state, and base. Create a branch, commit, push, or track `.agent/temp/` only when explicitly requested. Never let `/clarify` or `/answer` mutate VCS.

## 3. Execute And Verify

Per PBI:

1. Read target code and established patterns.
2. Implement the smallest complete unit.
3. Apply Clean Code and repository conventions.
4. Add/update required tests.
5. Run build/tests only when the Command Execution Authorization Gate permits; unit before integration.
6. On failure, fix and reverify; stop after two failed attempts.

After all PBIs:

1. Use allowed commands; otherwise static verification and report `not run per repo rule`.
2. Run `/review` on implemented changes and resolve Critical and Major findings.
3. Verify Priority Stack, Clean Code, compatibility, docs, and release-note impact.
4. Run `git diff --check`.

## 4. Walkthrough And Completion

Use the handoff's canonical `.agent/temp/WALKTHROUGH-<name>.md`; its basename must match `DEVELOP-<name>.md`. Record `develop_artifact` in walkthrough frontmatter. Stop on missing/mismatched metadata and never overwrite a walkthrough owned by another handoff.

Record implemented PBIs, PBI-to-file/test status, verification, Priority Stack, decisions, and deviations. Set `status: implemented-pending-approval` only after changes, verification, and walkthrough complete. Set `implemented` and its ISO 8601 timestamp only after user approval.
