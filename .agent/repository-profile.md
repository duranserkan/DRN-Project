# Repository Profile - DRN-Project

> Local overlay for `AGENTS.md` and `.agent/rules/DiSCOS.md`. Keep those entry points portable. Rebuild this profile from the destination when porting. `.agent/temp/` is local workflow history.

## Snapshot

| Aspect | Detail |
|---|---|
| Type | .NET 11 framework plus DDD reference app |
| Architecture | Domain -> Infrastructure/Application -> Hosted |
| Frontend | Razor Pages, htmx, Bootstrap 5, Vite |
| Testing | DTT; unit, analyzer, then integration |

## Layout

Folders are flat; `DRN.slnx` groups them. `docs/solution-structure.md` owns roles. Paths are repository-relative.

| Area | Paths |
|---|---|
| Framework | `DRN.Framework.*` |
| Nexus | `DRN.Nexus.*`, host `DRN.Nexus.Hosted/` |
| Reference app | `Sample.*`, host `Sample.Hosted/` |
| Tests | `DRN.Test.*` |
| Infrastructure | `Docker/`, `docker-compose.yml`, `docker-compose.dcproj` |
| Agent and CI | `.agent/`, `.github/workflows/`, `.github/actions/` |
| Docs | `README.md`, `ROADMAP.md`, `SECURITY.md`, `NOTICE.md`, `docs/` |

`docs/quick-start.md` owns setup and commands. Root README links standalone guides; its TOC links only its own sections. `Sample.Hosted/README.md` owns host/frontend usage. Module READMEs and RELEASE-NOTES own package docs.

Tooling uses net10.0: Hosting.Generators, SharedKernel.Analyzers/CodeFixes. Apps use net11.0. See package READMEs. Maps: `Helpers/AccessorDeclarations.cs`.

## Skill Routing

- Framework: `overview-drn-framework`, then the relevant `drn-*` skill.
- Testing: `drn-testing` and `overview-drn-testing` before generic `test-*` routers.
- Frontend: relevant `drn-buildwww-*` skills. Razor: `frontend-razor-pages-shared` -> `frontend-razor-pages-navigation` -> `frontend-razor-accessors`. DRN accessor construction: `drn-hosting`, Page & Endpoint Management.
- Package docs: `.agent/workflows/documentation.md`.
- Classification rules: `.agent/workflows/data-classification.md` with `drn-utils`, `drn-sharedkernel`, and `drn-testing`.

### Framework Skills

- Full DRN load set: `overview-drn-framework`, `overview-drn-testing`, `drn-sharedkernel`, `drn-entityframework`, `drn-domain-design`, `drn-utils`, `drn-hosting`, `drn-testing`, `drn-buildwww-libraries`, `drn-buildwww-packages`, `drn-buildwww-vite`, `drn-buildwww-react`.
- Overview set: `overview-drn-framework`, `overview-drn-testing`.
- Testing set: `drn-testing`, `overview-drn-testing`.

### Missing Profile Extensions

Use the [Stage 3 contract](workflows/update-execute.md#skill-discovery-and-custom-routes) for missing load-set skills.

## Documentation Modules

Default `/documentation` scope:

- `DRN.Framework.SharedKernel`
- `DRN.Framework.Utils`
- `DRN.Framework.EntityFramework`
- `DRN.Framework.Hosting`
- `DRN.Framework.Testing`
- `DRN.Framework.Jobs`
- `DRN.Framework.MassTransit`

`/optimize docs` and `/optimize all` include these modules' READMEs/release notes, root docs, Quick Start, Solution Structure, and the Sample guide.

## Commands

Apply the AGENTS.md command gate to npm and dotnet. Plan builds and tests only with permission. Run unit and analyzer tests before integration tests. Run MTP projects individually.

```bash
dotnet build DRN.slnx
dotnet run --project DRN.Test.Unit/DRN.Test.Unit.csproj
dotnet run --project DRN.Test.Analyzer/DRN.Test.Analyzer.csproj
dotnet run --project DRN.Test.Integration/DRN.Test.Integration.csproj
```

## Conventions

- DI/configuration: use DRN attributes, not duplicate manual registrations. See `overview-drn-framework` and `drn-utils`.
- SharedKernel `CONVENTIONS.md` owns application conventions. Declare alternate or ambiguous context destinations. Shared dependencies need no naming root or custom properties.
- Identity: require explicit `NexusAppSettings:AppId` and `AppInstanceId` in all environments. Zero is valid. Use Source-Known IDs and entity metadata. DTOs derive from `Dto` in `*.Contract` and expose `Guid` IDs, not entities. `drn-sharedkernel` and `drn-utils` own identity, time and lifecycle contracts.
- IDs: use `[Sensitive]` or `[Personal]` only when a contract, classification rule or reviewed use case requires it.
- Runtime access: prefer `[UnsafeAccessor]` for compile-time accessible types. Reserve reflection for dynamically discovered types.
- `drn-hosting` owns typed roots, `ConfigureEndpointCollection`, and automatic Razor detection.
- Logging: use `IScopedLog` for request/operation diagnostics. Reserve `ILogger` for bootstrap, scope flushing or audit events. Avoid duplicating request logs in audits. Applications own dynamic classification. Backend capture has no name-based redaction fallback.
- Testing: use `DrnTestContextUnit` in `DRN.Test.Unit`; full `DrnTestContext` belongs in integration tests. Reusable hosted test programs belong in `DRN.Test.Utils`, not MTP executables. `drn-testing` owns data attributes and fixture contracts. Do not weaken production invariants for tests.
- Frontend: `Sample.Hosted/buildwww/` builds to `Sample.Hosted/wwwroot/`. See [assets](../Sample.Hosted/README.md#frontend-assets). Browser APIs use `window.DRN`. Preserve `DRN`, `Drn*`, and `DRN.Framework.*`. Hosting owns typed static-file header rules and robots.txt revalidation. See `drn-hosting`.
- Sample behaviors: `buildwww/app/ts/behaviors/{layout,home,test}/` feed separate `layoutBehaviors.ts`, `homeBehaviors.ts`, and `testBehaviors.ts` entries. Load layout globally and page entries through their owning Razor `Scripts` sections. Use declarative data and idempotent mounting. `Sample.Hosted/README.md`, Independently updating component behaviors, owns aggregate/selective commands, nonce-fixture exceptions, and deployment/restart rules.
- Sample developer tools: completed MFA is required everywhere. Development allows signed-in users; other environments require `SystemAdmin` or boolean `SwaggerAccess`/`TestPagesAccess` claims set to `true`. The matching policies govern endpoints and visibility. Framework Swagger remains Development-only by default.
- Sample/Nexus configure `/Api` for unmatched API routes. Sample opts into HTMX Problem Details through `DrnApiErrorOptions.HandleHtmxRequests`. Preserve browser account isolation and separate `requestId`/`clientErrorId` references.
- Git: GitFlow-inspired. Integration branch `develop`, release branch `master`. Squash merge to `develop`; merge commits from `develop` to `master`. Tags: `release/v*.*.*` or `release/v*.*.*-previewNNN`.
- Security: CSP nonces, CSRF, and input validation are required. Pin third-party Actions to full immutable SHAs with version comments and verify upstream tag alignment. Pin container bases to verified registry digests. Keep SDK/runtime distro choices and `DOTNET_RUNTIME_VERSION` aligned with `RuntimeFrameworkVersion`.
- PR CI: trusted workflow/action code comes from `github.event.pull_request.base.sha` at workspace root; PR source lives under `src`. Jobs are secretless, have timeouts, and join an aggregate gatekeeper. `.github/CODEOWNERS` owns itself, workflows, and composite actions. Branch rulesets should require code-owner review and scanning/gatekeeper statuses.
- Release CI: derive one version from the release tag and verify its commit equals protected branch HEAD. Keep artifacts in the publishing job unless crossing jobs is necessary. Preserve pre-publish NuGet packages for provenance/SBOM checks. Verify NuGet.org's repository signature before removing `.signature.p7s` from a copy for GitHub attestation verification. Scan exact staged Docker digests, attest multi-platform provenance, bind SPDX 2.3 platform SBOMs to image digests, publish packages, then promote scanned digests. Use deterministic `staged-*` cleanup tags and a digest-pinned scanner.
- CodeRabbit loads `AGENTS.md` and DiSCOS through `knowledge_base.code_guidelines.filePatterns`.
- Workflow artifacts: `/clarify` owns `CLARIFY-*`; `/answer` accepts reviewed CLARIFY input and may resume approved `clarified` input solely to create collision-safe `DEVELOP-*`; `/develop` owns the matching `WALKTHROUGH-*`. Keep these in `.agent/temp/` and link them from system plans.

## Frontend Test Scope

In `Sample.Hosted/buildwww/tests/`, Node discovers `.test.js` and `.test.ts` recursively. CI reports frontend and TSX coverage. Master and release require LCOV and the Sonar gate. Playwright runs `browser/` fixtures against the built assets in a separate required job. `hosted/` needs a running Sample. See `Sample.Hosted/README.md`, Frontend Tests. Execution requires explicit permission.

## DRN Framework Source Overlay

### Source Map

Use these owners for public contracts. For implementation work, discover relevant source within the named module instead of copying implementation details into this profile.

| Contract | Owner | Agent guidance |
|---|---|---|
| Identity, entities, aggregate rules, repositories, analyzer diagnostics | `DRN.Framework.SharedKernel/README.md`, `DRN.Framework.EntityFramework/README.md` | `drn-sharedkernel`, `drn-entityframework` |
| Configuration, data paths, cancellation, ID/time utilities, events | `DRN.Framework.Utils/README.md` | `drn-utils`, `overview-drn-framework` |
| Classification, capture limits, exemptions, migration | `DRN.Framework.Utils/Logging/Diagnostics/README.md`; catalog `DRN.Framework.Utils/Logging/Diagnostics/data-classification-rules.json` | `drn-utils`, `drn-sharedkernel`, `drn-testing` |
| Hosting, SourceKnown policy enforcement, MFA, Razor and assets | `DRN.Framework.Hosting/README.md` | `drn-hosting`, relevant frontend skills |
| Test contexts, application factories, ownership | `DRN.Framework.Testing/README.md` | `drn-testing`, `overview-drn-testing` |
| Browser errors, notifications, developer access | `Sample.Hosted/README.md`; `Sample.Hosted/buildwww/types/drnProblem.types.ts`, `drnNotifications.types.ts` | `drn-buildwww-libraries` |

### Documentation Sync

Document public usage, behavior, configuration and migration. For shared changes, update owning READMEs and skills, assess release notes, and scan names/defaults/examples for drift. Align browser declarations with the Sample guide. Run `git diff --check`.

### Release Notes

Describe net consumer impact since the last published release (`release/v0.10.0` for upcoming 0.10.1). Preserve published history. Do not list documentation edits, README/release-note packaging edits, or fixes to bugs introduced after that baseline. Describe new features in final form.

Internal refactors, tests, comments, agent docs, and dependency-only updates without consumer impact need no entry outside coordinated releases. Dependency/runtime/container changes require notes when breaking, security-relevant, consumer-visible, or affecting published artifacts. Coordinated releases add the version to every Documentation Modules package, even without package-specific changes. Use concise alignment wording, e.g. `Dependencies upgraded to dotnet 10.0.11`.

NuGet notes contain the first `## Version ...` section, ending at the next version or `---` footer, plus canonical DiSC OS attribution and the bold signature below. Missing version sections or metadata over 35,000 characters fail packing. The packed file retains full history, prefix, and footer. `Directory.Build.targets` owns extraction.

### Package Release Notes Format

Use `## Version X.Y.Z`, omit empty sections, and preserve historical wording except when repairing invariant prefix/footer errors.

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
