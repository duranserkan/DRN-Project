---
name: frontend-buildwww-vite
description: Frontend build system - Vite multi-build configuration, TypeScript setup with path aliases, build output structure (wwwroot), and entry point management (appPreload, appPostload). Essential for frontend asset compilation and bundling. Keywords: vite, typescript, bundling, asset-compilation, npm, javascript, css, scss, build-pipeline, entry-points, manifest
last-updated: 2026-04-16
difficulty: intermediate
tokens: ~2K
---

# Sample.Hosted buildwww & Vite

> Frontend build system using Vite and TypeScript for Sample.Hosted.

## When to Apply
- Configuring frontend build
- Adding new JavaScript/TypeScript files
- Modifying Vite build configuration
- Working with path aliases
- Understanding build output structure

---

## Directory Structure

```
Sample.Hosted/
├── buildwww/                # Source files (not served)
│   ├── app/                 # Application code
│   │   ├── js/              # JavaScript modules
│   │   │   ├── drn/         # DRN utilities
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
        plugins: [stripHtmxEval()],
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
        }
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

> See [frontend-buildwww-react](../frontend-buildwww-react/SKILL.md) for the full React build architecture (Shadow DOM, Tailwind 4, IIFE format rationale).

### Build Commands

```bash
# Build all targets
npm run build:app
npm run build:appPostload
npm run build:htmx
npm run build:bootstrap
npm run build:react
```

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
            '@scss': resolve(__dirname, 'buildwww/scss'),
            '@css': resolve(__dirname, 'buildwww/css'),
            '@js': resolve(__dirname, 'buildwww/js'),
            '@ts': resolve(__dirname, 'buildwww/ts'),
            '@plugins': resolve(__dirname, 'buildwww/plugins')
        }
    }
};
// Per-build configs are deep-merged with sharedConfig
export default defineConfig(drnUtils.deepMerge(sharedConfig, builds[buildType]));
```

> **Note**: TypeScript path alias `@/*` → `buildwww/*` is configured in `tsconfig.json` and resolved by `moduleResolution: "bundler"`. The Vite `resolve.alias` entries above are for non-TypeScript (JS/CSS/SCSS) imports.

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
      "@scss/*": ["buildwww/app/scss/*"],
      "@types/*": ["buildwww/types/*"]
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
- [frontend-buildwww-packages.md](../frontend-buildwww-packages/SKILL.md)

---

## Entry Points

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
│       ├── reactBundle.[hash].css
│       └── .vite/manifest.json
```

---

## Using Built Assets in Razor

```razor
<script src="buildwww/app/js/appPreload.js"></script>
<link href="buildwww/lib/bootstrap/bootstrap.scss" rel="stylesheet" />
<script src="buildwww/lib/htmx/htmxBundle.js"></script>
```

---

## Related Skills

- [overview-ddd-architecture.md](../overview-ddd-architecture/SKILL.md) - Sample architecture
- [frontend-buildwww-libraries.md](../frontend-buildwww-libraries/SKILL.md) - Library usage
- [frontend-buildwww-react.md](../frontend-buildwww-react/SKILL.md) - React build architecture
- [frontend-buildwww-packages.md](../frontend-buildwww-packages/SKILL.md) - Package dependencies
- [frontend-razor-pages-shared.md](../frontend-razor-pages-shared/SKILL.md) - Layout integration

---
