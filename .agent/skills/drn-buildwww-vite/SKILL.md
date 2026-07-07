---
name: drn-buildwww-vite
description: "DRN buildwww Vite build system - multi-build configuration, TypeScript aliases, wwwroot output, appPreload/appPostload, and Vite manifest discovery. Keywords: drn, buildwww, vite, typescript, bundling, asset-compilation, npm, javascript, css, scss, build-pipeline, entry-points, manifest"
last-updated: 2026-07-07
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

---

## Directory Structure

```
<frontend-package>/
├── buildwww/                # Source files (not served)
│   ├── app/                 # Application code
│   │   ├── js/              # JavaScript modules
│   │   │   ├── drn/         # Application utilities
│   │   │   ├── appPreload.js
│   │   │   └── appPostload.js
│   │   └── css/             # Application CSS
│   ├── lib/                 # Library code
│   │   ├── htmx/            # htmx bundle
│   │   ├── bootstrap/       # Bootstrap customization
│   │   └── react/           # React mounted islands (Shadow DOM + Tailwind)
│   ├── plugins/             # Vite plugins
│   └── types/               # TypeScript declarations
├── wwwroot/                 # Built output (served)
│   ├── app/                 # Built app files
│   └── lib/                 # Built library files
├── vite.config.js           # Vite configuration
├── tsconfig.json            # TypeScript configuration
├── package.json             # npm dependencies
└── package-lock.json
```

---

## Vite Configuration

### Multi-Build Setup

```javascript
// vite.config.js — uses rolldownOptions (Vite 6+ with Rolldown)
const builds = {
    app: {
        plugins: [iifeWrap()],
        build: {
            outDir: 'wwwroot/app',
            rolldownOptions: {
                input: {
                    app: resolve(__dirname, 'buildwww/app/css/app.css'),
                    appPreload: resolve(__dirname, 'buildwww/app/js/appPreload.js')
                }
            }
        }
    },
    appPostload: {
        plugins: [iifeWrap()],
        build: {
            outDir: 'wwwroot/appPostload',
            rolldownOptions: {
                input: {
                    appPostload: resolve(__dirname, 'buildwww/app/js/appPostload.js')
                }
            }
        }
    },
    htmx: {
        plugins: [iifeWrap(), stripHtmxEval()],
        build: {
            outDir: 'wwwroot/lib/htmx',
            rolldownOptions: {
                input: {
                    htmxBundle: resolve(__dirname, 'buildwww/lib/htmx/htmxBundle.js')
                }
            }
        }
    },
    bootstrap: {
        build: {
            outDir: 'wwwroot/lib/bootstrap',
            rolldownOptions: {
                input: {
                    bootstrap: resolve(__dirname, 'buildwww/lib/bootstrap/bootstrap.scss'),
                    bootstrapBundle: resolve(__dirname, 'buildwww/lib/bootstrap/bootstrapBundle.js')
                }
            }
        },
        plugins: [iifeWrap()]
    },
    react: {
        plugins: [react(), tailwindcss()],
        build: {
            outDir: 'wwwroot/lib/react',
            rolldownOptions: {
                input: {
                    reactBundle: resolve(__dirname, 'buildwww/lib/react/reactBundle.tsx')
                },
                output: { format: 'iife', name: 'DrnReactMicroFrontend' }
            }
        }
    }
};

// Select build via environment variable
const buildType = process.env.BUILD_TYPE || 'app';
```

> See [drn-buildwww-react](../drn-buildwww-react/SKILL.md) for the full React build architecture (Shadow DOM, Tailwind 4, IIFE format rationale).

### Build Commands

Repository rule: do not run build commands unless the user explicitly allows them.

```bash
# Build all targets
npm run build

# Or build individual targets
npm run build:app
npm run build:appPostload
npm run build:htmx
npm run build:bootstrap
npm run build:react
```

CI validation runs `.github/actions/frontend-build` in parallel with backend checks. Release workflows run it before .NET build/test and publishing so generated `wwwroot` assets stay in the release job workspace.

### Shared Configuration

```javascript
const sharedConfig = {
    base: '/',
    build: {
        emptyOutDir: true,
        manifest: true,
        rolldownOptions: {
            output: {
                entryFileNames: `[name].[hash:16].js`,
                chunkFileNames: `[name].[hash:16].js`,
                assetFileNames: `[name].[hash:16].[ext]`
            }
        }
    },
    resolve: {
        alias: {
            '@css': resolve(__dirname, 'buildwww/app/css'),
            '@js': resolve(__dirname, 'buildwww/app/js'),
            '@lib': resolve(__dirname, 'buildwww/lib'),
            '@types': resolve(__dirname, 'buildwww/types'),
            '@plugins': resolve(__dirname, 'buildwww/plugins')
        }
    }
};
// Per-build configs are deep-merged with sharedConfig
export default defineConfig(drnUtils.deepMerge(sharedConfig, builds[buildType]));
```

> **Note**: TypeScript path alias `@/*` → `buildwww/*` is configured in `tsconfig.json` and resolved by `moduleResolution: "bundler"`. The Vite `resolve.alias` entries above mirror the actual `buildwww/app`, `buildwww/lib`, `buildwww/types`, and `buildwww/plugins` layout. No root `buildwww/js`, `buildwww/css`, `buildwww/ts`, or `buildwww/scss` directories are assumed.

---

## TypeScript Configuration

```json
{
  "compilerOptions": {
    "target": "ES2023",
    "module": "ESNext",
    "moduleResolution": "bundler",
    "strict": true,
    "noEmit": true,
    "allowJs": true,
    "allowImportingTsExtensions": true,
    "verbatimModuleSyntax": true,
    "lib": ["ES2023", "DOM", "DOM.Iterable"],
    "baseUrl": ".",
    "paths": {
      "@/*": ["buildwww/*"],
      "@js/*": ["buildwww/app/js/*"],
      "@css/*": ["buildwww/app/css/*"],
      "@lib/*": ["buildwww/lib/*"],
      "@types/*": ["buildwww/types/*"],
      "@plugins/*": ["buildwww/plugins/*"]
    },
    "typeRoots": ["./buildwww/types", "./node_modules/@types"]
  },
  "include": [
    "buildwww/app/**/*",
    "buildwww/lib/**/*",
    "buildwww/types/**/*"
  ],
  "exclude": ["node_modules", "wwwroot/dist"]
}
```

---

## Package Configuration
 
Detailed package versioning and dependency definitions are managed in:
- [drn-buildwww-packages.md](../drn-buildwww-packages/SKILL.md)

---

## Entry Points

Current builds are `app`, `appPostload`, `htmx`, `bootstrap`, and `react`.

### appPreload.js
Loaded early, before page content:
- Critical initializations
- Theme setup
- Cookie consent checks

### appPostload.js
Loaded after page content:
- Event handlers
- DOM manipulations
- Non-critical scripts

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
<script src="buildwww/app/js/appPreload.js"></script>
<link href="buildwww/app/css/app.css" rel="stylesheet" />
<link href="buildwww/lib/bootstrap/bootstrap.scss" rel="stylesheet" />
<script src="buildwww/lib/htmx/htmxBundle.js"></script>
<script src="buildwww/lib/bootstrap/bootstrapBundle.js"></script>
<script src="buildwww/app/js/appPostload.js"></script>
<script src="buildwww/lib/react/reactBundle.tsx"></script>
```

TagHelpers resolve these source paths through the per-output `.vite/manifest.json` files and emit content-hashed `wwwroot` paths with SRI where applicable.

Runtime manifest discovery is source-owned by `DRN.Framework.Hosting.Utils.Vite.ViteManifest`. DRN Hosting currently discovers Vite's default `.vite/manifest.json` files below the active web root, or `ContentRootPath/wwwroot` when the web root is empty. When changing environment defaults, publish behavior, or static-web-asset roots, verify that the running app can still see the Vite manifests; a rendered page can otherwise be missing CSS/JS even when server startup succeeds.

---

## Related Skills

- [overview-ddd-architecture.md](../overview-ddd-architecture/SKILL.md) - Architecture guidance
- [drn-buildwww-libraries.md](../drn-buildwww-libraries/SKILL.md) - Library usage
- [drn-buildwww-react.md](../drn-buildwww-react/SKILL.md) - React build architecture
- [drn-buildwww-packages.md](../drn-buildwww-packages/SKILL.md) - Package dependencies
- [frontend-razor-pages-shared.md](../frontend-razor-pages-shared/SKILL.md) - Layout integration

---
