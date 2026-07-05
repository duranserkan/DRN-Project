# Repository Profile - DRN-Project

> Local overlay for this repository. Keep `AGENTS.md` generic; keep DRN framework skills reusable.

## Snapshot

| Aspect | Detail |
|---|---|
| Type | .NET 10 framework plus DDD reference app |
| Architecture | Domain -> Infrastructure/Application -> Hosted |
| Frontend | Razor Pages + htmx + Bootstrap 5; Vite-built assets |
| Testing | DTT; integration-first with Testcontainers |

## Layout

Physical project folders are flat at the repository root. `DRN.slnx` supplies logical IDE groups.

| Area | Physical folders | Solution folder |
|---|---|---|
| Framework packages | `DRN.Framework.*` | `/Src/Framework/` |
| Nexus service | `DRN.Nexus.*` | `/Src/Nexus/` |
| Reference app | `Sample.*` | `/Src/Sample/` |
| Tests | `DRN.Test.*` | `/Test/` |
| Infrastructure | `Docker/`, `docker-compose.yml`, `docker-compose.dcproj` | `/Docker/` |
| Agent and CI | `.agent/`, `.github/workflows/`, `.github/actions/` | `/Items/` |
| Docs | `README.md`, `ROADMAP.md`, `SECURITY.md`, `NOTICE.md`, `docs/` | `/Docs/` |

Hosted projects: `Sample.Hosted/` for the reference app; `DRN.Nexus.Hosted/` for Nexus. Framework package docs live in each `DRN.Framework.*` folder as `README.md`, `RELEASE-NOTES.md`, and package metadata when present.

## Skill Routing

- DRN framework: load `overview-drn-framework`, then the relevant `drn-*` skill.
- Testing: load `drn-testing` and `overview-drn-testing` before generic `test-*` router skills.
- Frontend: load relevant `drn-buildwww-*` skills for DRN buildwww work and relevant `frontend-razor-*` skills for Razor UI; package root is `Sample.Hosted/`.
- Framework package docs: use `.agent/workflows/documentation.md`.

### Framework Skills

- Full DRN load set: `overview-drn-framework`, `overview-drn-testing`, `drn-sharedkernel`, `drn-entityframework`, `drn-domain-design`, `drn-utils`, `drn-hosting`, `drn-testing`, `drn-buildwww-libraries`, `drn-buildwww-packages`, `drn-buildwww-vite`, `drn-buildwww-react`.
- Overview set: `overview-drn-framework`, `overview-drn-testing`.
- Testing set: `drn-testing`, `overview-drn-testing`.

### Missing Profile Extensions

When copied to another repository, keep profile-declared custom skills that are missing under `.agent/skills/` as `⚠️ Missing profile reference` warnings. Generated loaders must include only existing skill directories. Resolve a missing reference by copying the skill or removing/updating the profile entry after review.

When missing custom skills exist, record them under the affected load-set section in a table with columns in this order: `Skill`, `Expected Loader`, `Status`, `Resolution`. Only rows whose `Status` is exactly `⚠️ Missing profile reference` satisfy Stage 0 verification. Keep missing skills out of generated loaders, `load-skills-all.md`, and loader union validation until the skill directory exists. Do not keep placeholder rows.

## Documentation Modules

When no narrower module is requested, `/documentation` targets:

- `DRN.Framework.SharedKernel`
- `DRN.Framework.Utils`
- `DRN.Framework.EntityFramework`
- `DRN.Framework.Hosting`
- `DRN.Framework.Testing`
- `DRN.Framework.Jobs`
- `DRN.Framework.MassTransit`

## Commands

Do not run build or test commands unless the user explicitly allows it.

```bash
dotnet build DRN.slnx
dotnet run --project DRN.Test.Unit/DRN.Test.Unit.csproj
dotnet run --project DRN.Test.Integration/DRN.Test.Integration.csproj
```

When test execution is explicitly allowed, run unit tests before integration tests.

## Conventions

- DI: use attributes; do not manually register attribute-decorated services. `[Scoped<T>]`, `[Singleton<T>]`, and `[Transient<T>]` declare lifetimes. `[Config("Section")]` and `[ConfigRoot]` bind configuration. `[HostedService]` declares background services.
- Entities: use Source-Known IDs with `long` internal IDs and `Guid` external IDs. Add `[EntityType(byte)]` to every `SourceKnownEntity`.
- DTOs: derive from `Dto`, live in `*.Contract`, expose `Guid` IDs only, and return DTOs from APIs. Do not return entities.
- Testing: use `[Fact]` without inline data or generated parameters. Use `[DataInline]` / `[DataInlineUnit]`, `[DataMember]` / `[DataMemberUnit]`, and `DataSelfAttribute` / `DataSelfUnitAttribute` for inline, member, and generated self data. Request `DrnTestContext` / `DrnTestContextUnit` only when needed; inline/member/self data follows the optional context, and AutoFixture/NSubstitute fill remaining parameters. Never use `DrnTestContext` in `DRN.Test.Unit`; use `DrnTestContextUnit` there and place full `DrnTestContext` coverage in `DRN.Test.Integration`. List unit tests before integration tests. When test execution is explicitly allowed, run MTP projects with `dotnet run --project <test-csproj>`; do not use `.slnx` for test runs. Keep reusable `WebApplicationFactory<TProgram>` entry-point programs in non-test support assemblies such as `DRN.Test.Utils`, not inside the MTP test executable.
- Frontend: Vite source lives in `Sample.Hosted/buildwww/`; built assets live under `Sample.Hosted/wwwroot/`. Browser utilities live under `window.DRN`; Vite imports `buildwww/app/js/drn/drnUtils.js` as `drnUtils`. `DRN`, `Drn*`, and `DRN.Framework.*` are shared framework surface, not placeholders. React islands use `buildwww/types/DrnReactTypes.ts`, `.drn-react-root`, and the `DrnReactMicroFrontend` IIFE wrapper. `NonceTagHelper` injects CSP nonces. CSRF is auto-added on `hx-post`, `hx-put`, `hx-delete`, and `hx-patch`.
- Git: use GitFlow-inspired flow. Merge `develop` to `master`. Tag releases as `release/v*.*.*` or `release/v*.*.*-previewNNN`. Squash merge to `develop`; use merge commits to `master`.
- CI/CD: PR workflows use split checkout: trusted workflow/composite action code from `github.event.pull_request.base.sha` at workspace root; PR source under `src`. PR jobs stay secretless, set explicit `timeout-minutes`, and join through an aggregate gatekeeper. Release workflows derive one version from `release/v...` tags, verify the tag commit equals protected source branch HEAD, keep build artifacts in the publishing job unless artifacts must cross jobs, stage Docker images with deterministic `staged-*` cleanup tags, scan the exact staged digest, publish packages, then promote Docker tags from scanned digests.
- Security: require CSP nonces, CSRF anti-forgery, and input validation. Pin third-party GitHub Actions to full commit SHAs with version comments; verify tag-to-SHA mapping from the official upstream action repository. `.github/CODEOWNERS` owns itself, workflows, and composite actions. Branch rulesets should require code-owner review and code-scanning/gatekeeper statuses. CodeRabbit global rules come from `knowledge_base.code_guidelines.filePatterns` for `AGENTS.md` and `.agent/rules/DiSCOS.md`. See `basic-security-checklist`, `overview-github-actions`, and `drn-hosting`.
- Containers: keep Docker SDK/runtime distro choices and `DOTNET_RUNTIME_VERSION` aligned with `RuntimeFrameworkVersion` metadata.
- Workflow artifacts: `/clarify`, `/answer`, and `/develop` must generate or update workspace-local artifacts such as `CLARIFY-*.md` and `DEVELOP-*.md` in `.agent/temp/`. System plans must reference and link those pipeline documents.

## DRN Framework Source Overlay

Framework conventions and defaults live in framework-scoped DRN skills, especially `overview-drn-framework`. Before changing shared framework facts, check the source-owned files below.

### Source Map

| Shared fact | Source of truth | Main consumers |
|---|---|---|
| Configuration source order | [ConfigurationExtensions.cs](../DRN.Framework.Hosting/Extensions/ConfigurationExtensions.cs) | Utils README, Hosting README, DRN framework skills |
| Mounted settings paths | [MountedSettingsConventions.cs](../DRN.Framework.Utils/Settings/Conventions/MountedSettingsConventions.cs) | Utils README, Hosting README |
| Development settings defaults | [DrnDevelopmentSettings.cs](../DRN.Framework.Utils/Settings/DrnDevelopmentSettings.cs) | EntityFramework README, Testing README |
| Test settings convention | [SettingsProvider.cs](../DRN.Framework.Testing/Providers/SettingsProvider.cs) | Testing README, testing skills |
| Vite manifest discovery and publish support | [ViteManifest.cs](../DRN.Framework.Hosting/Utils/Vite/ViteManifest.cs), [DRN.Framework.Hosting.targets](../DRN.Framework.Hosting/buildTransitive/DRN.Framework.Hosting.targets) | Hosting README, frontend Vite skill, DRN hosting skill |

### Documentation Sync

When source code changes a shared framework fact:

1. Verify the source-owned file in the source map.
2. Update package READMEs wherever package readers need the new behavior.
3. Update owning `DRN.Framework.*` release notes when published behavior, public contract, security or operational default, observable bug fix, data/migration behavior, or published package metadata other than version-only alignment changed.
4. Update framework-scoped DRN skills used for that package.
5. Search changed terms, renamed keys, changed defaults, and removed examples across package docs, framework skills, `AGENTS.md`, and this profile.
6. Run `git diff --check`.

Release notes are not required for internal-only refactors, tests, comments, agent-only docs, dependency-only updates with no consumer-visible impact, or shared-version release alignment for packages with no package-specific changes. Dependency/runtime/container changes require release notes when they are breaking, security-relevant, consumer-visible, or alter published package artifacts. During release preparation, if no package-specific change exists before release, add one concise version-alignment disclaimer only when package metadata would otherwise be empty. Outside release preparation, leave unchanged package `RELEASE-NOTES.md` files untouched and report release notes as not required; the standard prefix covers consistency-only version increments.
