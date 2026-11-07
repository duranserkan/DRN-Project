---
name: drn-buildwww-vite
description: "DRN Vite runtime and domain builds. Keywords: drn, buildwww, vite, typescript, bundles, manifest, cache"
last-updated: 2026-11-07
difficulty: intermediate
tokens: ~2K
---

# DRN buildwww & Vite

> Frontend build system using Vite and TypeScript for repositories that declare the DRN `buildwww` convention.

## When to Apply
- Configuring frontend build
- Adding new JavaScript/TypeScript files
- Modifying Vite build configuration
- Working with path aliases
- Understanding build output structure

## Directory Structure

```
<frontend-package>/
├── buildwww/                # Source files (not served)
│   ├── app/                 # Application code
│   │   ├── ts/              # TypeScript modules
│   │   │   ├── drn/         # Application utilities
│   │   │   ├── appPreload.ts
│   │   │   └── appPostload.ts
│   │   └── css/             # Application CSS
│   ├── lib/                 # Library code
│   │   ├── htmx/            # htmx bundle
│   │   ├── bootstrap/       # Bootstrap customization
│   │   └── react/           # React mounted islands (Shadow DOM + Tailwind)
│   ├── plugins/             # Vite plugins
│   └── types/               # Shared type contracts and ambient declarations
├── wwwroot/                 # Built output (served)
│   ├── app/                 # Built app files
│   └── lib/                 # Built library files
├── vite.config.ts           # Vite configuration
├── vite.config.plugin.ts    # Vite plugins
├── tsconfig.json            # TypeScript configuration
├── package.json             # npm dependencies
└── package-lock.json
```

---

## Vite Configuration

Follow [Package Imports](../drn-buildwww-packages/SKILL.md#package-imports) when choosing dependency paths. Verify stylesheet resolution before migrating existing Sass imports.

### Stylesheet conventions

- Use plain CSS for site styles, including overrides that target rendered Bootstrap components.
- Use CSS for Tailwind styles in React islands. Keep `@reference` and `@apply` in the [React CSS pipeline](../drn-buildwww-react/SKILL.md#css-strategy).
- Use SCSS to [customize the Bootstrap build](../drn-buildwww-libraries/SKILL.md#bootstrap-customization), such as setting Sass variables or selecting component imports.

### Multi-Build Setup

The frontend root's `vite.config.ts` defines each target's entry points, plugins, and output directory. Targeted npm scripts set `BUILD_TYPE`. The default target is `app`.

For classic-script builds with mixed or multiple entries, use `iifeWrap()` to isolate each JavaScript chunk and prevent name collisions between bundles. A single named JavaScript entry such as React can use native IIFE output. Keep `stripHtmxEval()` on the HTMX target to remove its direct `eval` call from the shipped bundle.

> See [drn-buildwww-react](../drn-buildwww-react/SKILL.md) for the full React build architecture (Shadow DOM, Tailwind 4, IIFE format rationale).

### Build Commands

Builds require explicit permission. Apply the AGENTS.md npm command gate to linting, formatting, type checking, and builds invoked through npm.

The full build runs `check` for formatting, script and stylesheet linting, type checking, and the advisory Knip report. Targeted builds skip this gate. The package manifest owns commands and lint scope.

```bash
# Build all targets
npm run build

# Or build individual targets
npm run build:app
npm run build:appPostload
npm run build:htmx
npm run build:bootstrap
npm run build:react
npm run build:behaviors
# Or build one behavior domain
npm run build:behaviors:layout
npm run build:behaviors:home
npm run build:behaviors:test
```


### Shared Configuration

Align aliases with `tsconfig.json` and buildwww. `@/*` maps to `buildwww/*` only through TypeScript `paths`; Vite does not resolve it. Keep `@/` imports type-only unless Vite has a matching `resolve.alias`. Discover paths rather than assuming root-level js/css folders.

---

## TypeScript Configuration

`tsconfig.json` owns compiler options, source scope, declarations, and aliases. Include the root Vite config and plugin TypeScript files. Align resolution and aliases with Vite. Do not duplicate compiler configuration. Type checking, like builds, requires execution authorization.

Follow [imports and types](references/type-ownership.md) and [test conventions](references/frontend-tests.md).

---

## Package Configuration
 
Package guidance:
- [drn-buildwww-packages.md](../drn-buildwww-packages/SKILL.md)

---

## Entry Points

Current builds are `app`, `appPostload`, `htmx`, `bootstrap`, `react`, `layoutBehaviors`, `homeBehaviors`, and `testBehaviors`.

### Domain Behavior Entries

Keep modules under `buildwww/app/ts/behaviors/{layout,home,test}/`. Each entry imports only its domain modules and calls existing `window.DRN`/Bootstrap APIs rather than importing runtime implementations. Read configuration from encoded element data and keep mount state/cleanup local.

| Entry | Output directory | Loading |
|---|---|---|
| `layoutBehaviors.ts` | `wwwroot/behaviors/layout` | Global shell after runtime bundles |
| `homeBehaviors.ts` | `wwwroot/behaviors/home` | HomeAnonymous page `Scripts` |
| `testBehaviors.ts` | `wwwroot/behaviors/test` | HTMX test page `Scripts` |

`build:behaviors` runs `build:behaviors:layout`, `build:behaviors:home`, and `build:behaviors:test` sequentially. Each script explicitly sets `BUILD_TYPE` to select its Vite target, writes a hashed entry and `.vite/manifest.json`, and cleans its own directory. The full build calls this aggregate. Unchanged inputs/tooling preserve unrelated bundle cache identities. Keep separate chunks and output roots.

Each entry polls after its imports' deferred registrations on initial load:

```javascript
if (document.readyState === 'loading')
    document.addEventListener('DOMContentLoaded', () => window.onmount(), {once: true});
```

Use a fresh callback so listener identity deduplication cannot reuse an earlier poll from postload. Late script execution registers/polls through `DRN.Onmount`; existing htmx polling handles swaps. Page script execution may repeat, so registration must remain idempotent. Runtime bundles, including React, stay global.

Deploy changed assets and hidden manifests together. Restart the app to refresh manifest/SRI caches; fully reload existing pages for changed registrations. Retain old hashed assets for in-flight pages. The initial migration includes Razor/postload changes and removes the obsolete `appBehaviors` output from the active deployment set. See the profile-declared application guide for deployment details. Intentional inline nonce test fixtures remain in Razor.

### appPreload.ts
Loaded early, before page content:
- Critical initializations
- Theme setup
- Cookie consent checks

### appPostload.ts
Loaded in the global shell's head. Installs application-wide event handlers and defers DOM-dependent work. Component registrations belong to their behavior domains.

---

## Output Structure

```
wwwroot/
├── app/
│   ├── appPreload.[hash].js
│   ├── app.[hash].css
│   └── .vite/manifest.json
├── appPostload/
│   ├── appPostload.[hash].js
│   └── .vite/manifest.json
├── behaviors/
│   ├── layout/               # layoutBehaviors.[hash].js + .vite/manifest.json
│   ├── home/                 # homeBehaviors.[hash].js + .vite/manifest.json
│   └── test/                 # testBehaviors.[hash].js + .vite/manifest.json
├── lib/
│   ├── htmx/
│   │   ├── htmxBundle.[hash].js
│   │   └── .vite/manifest.json
│   ├── bootstrap/
│   │   ├── bootstrap.[hash].css
│   │   ├── bootstrapBundle.[hash].js
│   │   └── .vite/manifest.json
│   └── react/
│       ├── reactBundle.[hash].js
│       └── .vite/manifest.json
```

---

## Using Built Assets in Razor

```razor
<script src="buildwww/app/ts/appPreload.ts"></script>
<link href="buildwww/app/css/app.css" rel="stylesheet" />
<link href="buildwww/lib/bootstrap/bootstrap.scss" rel="stylesheet" />
<script src="buildwww/lib/htmx/htmxBundle.ts"></script>
<script src="buildwww/lib/bootstrap/bootstrapBundle.ts"></script>
<script src="buildwww/app/ts/appPostload.ts"></script>
<script src="buildwww/lib/react/reactBundle.tsx"></script>
<script src="buildwww/app/ts/layoutBehaviors.ts"></script>
```

TagHelpers resolve these source paths through the per-output `.vite/manifest.json` files and emit content-hashed `wwwroot` paths with SRI where applicable.

Home/test entries belong in their owning page's `Scripts` section, not the global shell. Both full and htmx layout paths must render the section. Preserve normal script tag helper processing, response nonces, SRI, and dependency order. External scripts can finish after htmx's load event, so mount registration must also work when the page is already interactive.

The emitted script and link URLs include `Request.PathBase`, including manifest-backed preload links. Use a shared relative `base: './'` so generated CSS asset references and Vite-managed imports/preloads resolve relative to their output files under any proxy prefix. Keep manually authored application assets application-relative with `@Url.Content("~/images/favicon.ico")`. Non-Vite URLs pass through the Vite helpers unchanged.

Runtime manifest discovery is source-owned by `DRN.Framework.Hosting.Utils.Vite.ViteManifest`. DRN Hosting currently discovers Vite's default `.vite/manifest.json` files below the active web root, or `ContentRootPath/wwwroot` when the web root is empty. When changing environment defaults, publish behavior, or static-web-asset roots, verify that the running app can still see the Vite manifests; a rendered page can otherwise be missing CSS/JS even when server startup succeeds.

---

## Related Skills

- [overview-ddd-architecture.md](../overview-ddd-architecture/SKILL.md) - Architecture guidance
- [drn-buildwww-libraries.md](../drn-buildwww-libraries/SKILL.md) - Library usage
- [drn-buildwww-react.md](../drn-buildwww-react/SKILL.md) - React build architecture
- [drn-buildwww-packages.md](../drn-buildwww-packages/SKILL.md) - Package dependencies
- [frontend-razor-pages-shared.md](../frontend-razor-pages-shared/SKILL.md) - Layout integration

---
