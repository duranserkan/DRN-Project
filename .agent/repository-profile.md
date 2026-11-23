# Repository Profile - DRN-Project

> Local overlay for `AGENTS.md` and `.agent/rules/DiSCOS.md`. Keep entry points portable and rebuild this file when porting. `.agent/temp/` holds local workflow history.

## Snapshot

| Aspect | Detail |
|---|---|
| Type | .NET 11 framework + DDD reference app |
| Architecture | Domain -> Infrastructure/Application -> Hosted |
| Frontend | Razor Pages, htmx, Bootstrap 5, Vite |
| Testing | DTT: unit, analyzer, then integration |

## Layout

Flat folders grouped by `DRN.slnx` (roles: `docs/solution-structure.md`, setup/commands: `docs/quick-start.md`, host/frontend: `Sample.Hosted/README.md`, packages: module `README.md` + `RELEASE-NOTES.md`). Root README links standalone guides and its TOC links only its own sections.

| Area | Paths |
|---|---|
| Framework | `DRN.Framework.*` |
| Nexus | `DRN.Nexus.*`, host `DRN.Nexus.Hosted/` |
| Reference app | `Sample.*`, host `Sample.Hosted/` |
| Tests | `DRN.Test.*` |
| Infrastructure | `Docker/`, `docker-compose.yml`, `docker-compose.dcproj` |
| Agent and CI | `.agent/`, `.github/workflows/`, `.github/actions/` |
| Docs | `README.md`, `ROADMAP.md`, `SECURITY.md`, `NOTICE.md`, `docs/` |

Tooling targets `net10.0` (`Hosting.Generators`, `SharedKernel.Analyzers/CodeFixes`). Apps target `net11.0`. Accessor maps: `Helpers/AccessorDeclarations.cs`.

## Skill Routing

- Framework: `overview-drn-framework`, then relevant `drn-*`.
- Testing: `drn-testing` and `overview-drn-testing` before generic `test-*`.
- Frontend: relevant `drn-buildwww-*`. Razor: `frontend-razor-pages-shared` -> `frontend-razor-pages-navigation` -> `frontend-razor-accessors`. DRN accessors: `drn-hosting`.
- Package docs: `.agent/workflows/documentation.md`.
- Classification: `.agent/workflows/data-classification.md` with `drn-utils`, `drn-sharedkernel`, `drn-testing`.

### Framework Skills

- Full DRN load set: `overview-drn-framework`, `overview-drn-testing`, `drn-sharedkernel`, `drn-entityframework`, `drn-domain-design`, `drn-utils`, `drn-hosting`, `drn-testing`, `drn-buildwww-libraries`, `drn-buildwww-packages`, `drn-buildwww-vite`, `drn-buildwww-react`.
- Overview set: `overview-drn-framework`, `overview-drn-testing`.
- Testing set: `drn-testing`, `overview-drn-testing`.

### Missing Profile Extensions

See [Stage 3 contract](workflows/update-execute.md#skill-discovery-and-custom-routes).

## Documentation Modules

Default `/documentation` scope:

- `DRN.Framework.SharedKernel`
- `DRN.Framework.Utils`
- `DRN.Framework.EntityFramework`
- `DRN.Framework.Hosting`
- `DRN.Framework.Testing`
- `DRN.Framework.Jobs`
- `DRN.Framework.MassTransit`

`/optimize docs` and `/optimize all` include these modules' READMEs/release notes, root docs, Quick Start, Solution Structure, and Sample guide.

## Commands

Apply the `AGENTS.md` command gate to `npm` and `dotnet`. Plan builds/tests only with permission. Run unit and analyzer tests before integration tests, and run MTP projects individually.

```bash
dotnet build DRN.slnx
dotnet run --project DRN.Test.Unit/DRN.Test.Unit.csproj
dotnet run --project DRN.Test.Analyzer/DRN.Test.Analyzer.csproj
dotnet run --project DRN.Test.Integration/DRN.Test.Integration.csproj
```

## Conventions

- DI/config: use DRN attributes, not duplicate manual registrations (`overview-drn-framework`, `drn-utils`).
- Conventions: `DRN.Framework.SharedKernel/CONVENTIONS.md` owns rules and terminology. Declare alternate/ambiguous context destinations. Shared dependencies need no naming root or custom properties.
- Identity and contracts: require explicit `NexusAppSettings:AppId` and `AppInstanceId` everywhere (`0` is valid). Use Source-Known IDs and entity metadata. Internal Contract DTOs derive from `Dto` in `*.Contract` and map via `SourceKnownEntity.ToDto()`. Public HTTP endpoints use explicit `*Request`/`*Response` contracts exposing `Guid` IDs, never `Dto` or entities (`drn-sharedkernel`, `drn-utils`).
- IDs: apply `[Sensitive]` or `[Personal]` only when a contract, classification rule, or reviewed use case requires it.
- Runtime access: prefer `[UnsafeAccessor]` for compile-time accessible types and reflection only for dynamic types.
- Hosting: `drn-hosting` owns typed roots, `ConfigureEndpointCollection`, automatic Razor detection, static-file headers, and `robots.txt` revalidation.
- Logging: use `IScopedLog` for request/operation diagnostics and `ILogger` for bootstrap, scope flushing, or audit events. Avoid duplicating request logs in audits. Apps own dynamic classification, with no backend name-based redaction fallback.
- Testing: use `DrnTestContextUnit` in `DRN.Test.Unit` and full `DrnTestContext` in integration tests. Put reusable hosted test programs in `DRN.Test.Utils`, not MTP executables (`drn-testing`). Never weaken production invariants for tests.
- Frontend: `Sample.Hosted/buildwww/` builds to `Sample.Hosted/wwwroot/` ([assets](../Sample.Hosted/README.md#frontend-assets)). Browser APIs use `window.DRN`. Preserve `DRN`, `Drn*`, and `DRN.Framework.*`. `buildwww/app/ts/behaviors/{layout,home,test}/` feed matching `*Behaviors.ts` entries loaded globally (layout) or via owning Razor `Scripts` sections with declarative data and idempotent mounting (`Sample.Hosted/README.md` owns selective commands, nonce-fixture exceptions, and restart rules).
- Sample dev tools: require completed MFA. Swagger and HTMX test pages allow signed-in users in Development and require `SystemAdmin` or the matching boolean claim elsewhere. Use `[DeveloperExceptionPreview]` and `DeveloperExceptionAccess.AllowsAsync`. Framework Swagger defaults to Development.
- API errors: Sample/Nexus configure `/Api` for unmatched API routes. Sample enables `DrnApiErrorOptions.HandleHtmxRequests`. Preserve browser account isolation and separate `requestId`/`clientErrorId`.
- Git: GitFlow (`develop` integration, `master` release). Squash merge to `develop`, merge commit to `master`. Tags: `release/v*.*.*` or `release/v*.*.*-previewNNN`.
- Security: require CSP nonces, CSRF, input validation, SHA-pinned third-party Actions (with version comments and tag alignment), and digest-pinned container bases. Align SDK/runtime distros and `DOTNET_RUNTIME_VERSION` with `RuntimeFrameworkVersion`.
- PR/Release CI (`overview-github-actions`): PR workflows run trusted code from `github.event.pull_request.base.sha` at root with PR source under `src`, stay secretless with timeouts, and join the gatekeeper. `.github/CODEOWNERS` owns itself, workflows, and composite actions. Release CI derives one version from the tag, verifies commit == protected branch HEAD, keeps pre-publish NuGet packages for provenance/SBOM checks, verifies NuGet.org signatures before copying without `.signature.p7s` for GitHub attestation, scans staged Docker digests with a digest-pinned scanner, attests multi-platform provenance and SPDX 2.3 SBOMs, promotes scanned digests, and cleans `staged-*` tags.
- CodeRabbit loads `AGENTS.md` and DiSCOS via `knowledge_base.code_guidelines.filePatterns`.
- Workflow artifacts: `/clarify` -> `CLARIFY-*`, `/answer` -> reviewed CLARIFY or approved `clarified` solely to create collision-safe `DEVELOP-*`, `/develop` -> `WALKTHROUGH-*` in `.agent/temp/`, linked from system plans.

## Frontend Test Scope

`Sample.Hosted/README.md` (Frontend tests) owns CI gates, coverage, and commands. Master and verified release tags supply LCOV and built assets for combined Sonar analysis, Playwright, and hosted integration tests.

## DRN Framework Source Overlay

### Source Map

| Contract | Owner | Agent guidance |
|---|---|---|
| Identity, aggregates, repositories, conventions, diagnostics | `DRN.Framework.SharedKernel/README.md`, `DRN.Framework.SharedKernel/CONVENTIONS.md`, `DRN.Framework.SharedKernel/CONVENTION-DIAGNOSTICS.md`, `DRN.Framework.SharedKernel/SCAFFOLDING.md`, `DRN.Framework.EntityFramework/README.md` | `drn-sharedkernel`, `drn-domain-design`, `drn-entityframework` |
| Configuration, data paths, cancellation, ID/time utilities, events | `DRN.Framework.Utils/README.md` | `drn-utils`, `overview-drn-framework` |
| Classification, capture limits, exemptions | `DRN.Framework.Utils/Logging/Diagnostics/README.md`, `DRN.Framework.Utils/Logging/Diagnostics/data-classification-rules.json` | `drn-utils`, `drn-sharedkernel`, `drn-testing` |
| Hosting, SourceKnown policy enforcement, MFA, Razor and assets | `DRN.Framework.Hosting/README.md` | `drn-hosting`, relevant frontend skills |
| Test contexts, application factories, ownership | `DRN.Framework.Testing/README.md` | `drn-testing`, `overview-drn-testing` |
| Browser errors, notifications, developer access | `Sample.Hosted/README.md`, `Sample.Hosted/buildwww/types/drnProblem.types.ts`, `drnNotifications.types.ts` | `drn-buildwww-libraries` |

### Documentation Sync

Document public usage, behavior, and configuration in READMEs, with concise migration notes in release notes. For shared changes, update owning READMEs and skills, assess release notes, align browser declarations with Sample, check names/defaults/examples for drift, and run `git diff --check`.

### Release Notes

Describe net consumer impact since `release/v0.10.0` in final form while preserving published history. Omit unreleased-feature breaking changes/fixes, doc/packaging edits, internal refactors, tests, comments, agent docs, and non-impacting dependency updates outside coordinated releases. Note breaking, security-relevant, consumer-visible, or artifact-affecting dependency/runtime/container changes. Coordinated releases add the version to every Documentation Modules package with concise alignment wording (e.g., `Dependencies upgraded to dotnet 10.0.11`).

NuGet packing (`Directory.Build.targets`) extracts the first `## Version ...` section until the next version or `---` footer, plus canonical DiSC OS attribution and bold signature. Missing versions or >35,000 chars fail packing. Packed files retain full history, prefix, and footer.

### Package Release Notes Format

Use `## Version X.Y.Z`, omit empty sections, and preserve history except invariant prefix/footer repairs.

```markdown
Not every version includes changes, features or bug fixes. This project can increment version to keep consistency with other DRN.Framework projects.

## Version X.Y.Z

[Optional dedication or milestone message]

### Breaking Changes
* **Area**: Consumer impact.

### New Features
* **Feature**: Public behavior.

### Changed
* **Area**: Consumer impact.

### Bug Fixes
* **Area**: Observable fix.

---

Documented with the assistance of [DiSC OS](https://github.com/duranserkan/DRN-Project/blob/develop/.agent/rules/DiSCOS.md)

---
**Semper Progressivus: Always Progressive**
```
