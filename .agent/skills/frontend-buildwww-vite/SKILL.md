---
name: frontend-buildwww-vite
description: Frontend build system - Vite multi-build configuration, TypeScript setup with path aliases, build output structure (wwwroot), and entry point management (appPreload, appPostload). Essential for frontend asset compilation and bundling. Keywords: frontend, build-pipeline, vite, typescript, bundling, asset-compilation, npm, javascript, css, scss, skills, overview ddd architecture, frontend buildwww libraries, frontend razor pages shared
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
│   │   └── bootstrap/       # Bootstrap customization
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
// vite.config.js
const builds = {
    app: {
        build: {
            outDir: 'wwwroot/app',
            rollupOptions: {
                input: {
                    app_preload: resolve(__dirname, 'buildwww/app/js/appPreload.js'),
                    app_postload: resolve(__dirname, 'buildwww/app/js/appPostload.js')
                }
            }
        }
    },
    htmx: {
        build: {
            outDir: 'wwwroot/lib/htmx',
            rollupOptions: {
                input: {
                    htmx_bundle: resolve(__dirname, 'buildwww/lib/htmx/htmxBundle.js')
                }
            }
        }
    },
    bootstrap: {
        build: {
            outDir: 'wwwroot/lib/bootstrap',
            rollupOptions: {
                input: {
                    bootstrap: resolve(__dirname, 'buildwww/lib/bootstrap/bootstrap.scss'),
                    bootstrap_bundle: resolve(__dirname, 'buildwww/lib/bootstrap/bootstrap.js')
                }
            }
        }
    }
};

// Select build via environment variable
const buildType = process.env.BUILD_TYPE || 'app';
```

### Build Commands

```bash
# Build app
npm run build:app

# Build specific targets
npm run build:htmx
npm run build:bootstrap
```

### Shared Configuration

```javascript
const sharedConfig = {
    base: '/',
    build: {
        emptyOutDir: true,
        manifest: true,
        rollupOptions: {
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
```

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
│   ├── app_preload.[hash].js
│   ├── app_postload.[hash].js
│   └── .vite/manifest.json
├── lib/
│   ├── htmx/
│   │   ├── htmx_bundle.[hash].js
│   │   └── .vite/manifest.json
│   └── bootstrap/
│       ├── bootstrap.[hash].css
│       ├── bootstrap_bundle.[hash].js
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
- [frontend-razor-pages-shared.md](../frontend-razor-pages-shared/SKILL.md) - Layout integration

---
