---
name: frontend-buildwww-packages
description: 'Frontend package dependencies - Standardized npm packages (Bootstrap, htmx, aspnet-client-validation, onmount, React, Tailwind), version management, and dependency purposes. Reference for adding or updating frontend libraries. Keywords: npm, packages, dependencies, bootstrap, htmx, aspnet-client-validation, onmount, react, tailwind, package-management, versioning'
last-updated: 2026-06-07
difficulty: basic
tokens: ~0.5K
---

# Sample.Hosted npm Packages

> Definitions of standard frontend dependencies and their purposes.

## When to Apply
- Adding new npm libraries
- Updating existing dependencies
- Checking version compatibility
- Understanding the purpose of installed packages

---

## Source Of Truth

Use `Sample.Hosted/package.json` as the source of truth for npm scripts, package names, and pinned top-level versions. Use `Sample.Hosted/package-lock.json` as the source of truth for resolved dependency metadata.

Do not copy package versions into this skill. Hardcoded versions drift when dependency maintenance commits update `package.json`.

Top-level `dependencies` and `devDependencies` must use exact versions, not caret or tilde ranges. This keeps the supply-chain policy in `package.json` true and makes `npm ci` reproducible.

When changing packages:

1. Edit `Sample.Hosted/package.json` or run npm with exact-save behavior.
2. Refresh lock metadata with `npm install --prefix Sample.Hosted --package-lock-only --ignore-scripts`.
3. Verify the root lock metadata matches `package.json`.
4. Run `npm ci --prefix Sample.Hosted --ignore-scripts --dry-run` to verify install consistency.

```bash
node -e 'const fs=require("fs"); const p=JSON.parse(fs.readFileSync("Sample.Hosted/package.json","utf8")); const l=JSON.parse(fs.readFileSync("Sample.Hosted/package-lock.json","utf8")).packages[""]; let bad=[]; for (const kind of ["dependencies","devDependencies"]) for (const [k,v] of Object.entries(p[kind]||{})) { const lv=(l[kind]||{})[k]; if (v!==lv) bad.push(kind+" "+k+": package.json="+v+" lock="+lv); } if (bad.length) { console.log(bad.join("\n")); process.exit(1); }'
```

---

## Dependency Details

### Core Libraries
- **bootstrap**: UI framework for layout and styling.
- **bootstrap-icons**: Official icon set for Bootstrap.
- **@popperjs/core**: Positioning engine for tooltips and popovers (required by Bootstrap).
- **htmx.org**: Library for modern HTML-first interactivity.

### Validation & Utilities
- **aspnet-client-validation**: ASP.NET Core-compatible client-side validation.
- **onmount**: Lifecycle management for DOM elements (used by standard RSJS pattern).
- **react/react-dom**: Mounted island components for richer interactive surfaces.
- **tailwindcss**: Utility styling for the React bundle.

### Build & Dev Tooling
- **vite**: Next Generation Frontend Tooling.
- **sass**: CSS preprocessor.
- **typescript**: Typed JavaScript superset.
- **globals**: Global variable definitions (often used for linting contexts).
- **@tailwindcss/vite** and **@vitejs/plugin-react**: Vite plugins for Tailwind and React builds.

---

## Related Skills

- [frontend-buildwww-vite.md](../frontend-buildwww-vite/SKILL.md) - Build process configuration
- [frontend-buildwww-libraries.md](../frontend-buildwww-libraries/SKILL.md) - Library implementation and usage patterns
