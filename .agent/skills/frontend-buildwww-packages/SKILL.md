---
name: frontend-buildwww-packages
description: 'Frontend package dependencies - Standardized npm packages (Bootstrap, htmx, aspnet-client-validation, onmount, React, Tailwind), version management, and dependency purposes. Reference for adding or updating frontend libraries. Keywords: npm, packages, dependencies, bootstrap, htmx, aspnet-client-validation, onmount, react, tailwind, package-management, versioning'
last-updated: 2026-06-12
difficulty: basic
tokens: ~0.5K
---

# Frontend npm Packages

> Definitions of standard frontend dependencies and their purposes.

## When to Apply
- Adding new npm libraries
- Updating existing dependencies
- Checking version compatibility
- Understanding the purpose of installed packages

---

## Source Of Truth

Resolve the frontend package root from `.agent/repository-profile.md` when it declares one. Otherwise, discover it by searching for `package.json` and choosing the root that owns `vite.config.*`, `buildwww/`, `src/`, or frontend build scripts.

Use that root's `package.json` as the source of truth for npm scripts, package names, and pinned top-level versions. Use the sibling lockfile as the source of truth for resolved dependency metadata.

Do not copy package versions into this skill. Hardcoded versions drift when dependency maintenance commits update `package.json`.

Top-level `dependencies` and `devDependencies` must use exact versions, not caret or tilde ranges. This keeps the supply-chain policy in `package.json` true and makes `npm ci` reproducible.

When changing packages:

1. Set `FRONTEND_PACKAGE_DIR` to the discovered package root.
2. Edit `$FRONTEND_PACKAGE_DIR/package.json` or run npm with exact-save behavior.
3. Refresh lock metadata with `npm install --prefix "$FRONTEND_PACKAGE_DIR" --package-lock-only --ignore-scripts`.
4. Verify the root lock metadata matches `package.json`.
5. Run `npm ci --prefix "$FRONTEND_PACKAGE_DIR" --ignore-scripts --dry-run` to verify install consistency.

```bash
node -e 'const fs=require("fs"); const dir=process.argv[1]; const p=JSON.parse(fs.readFileSync(`${dir}/package.json`,"utf8")); const l=JSON.parse(fs.readFileSync(`${dir}/package-lock.json`,"utf8")).packages[""]; let bad=[]; for (const kind of ["dependencies","devDependencies"]) for (const [k,v] of Object.entries(p[kind]||{})) { const lv=(l[kind]||{})[k]; if (v!==lv) bad.push(kind+" "+k+": package.json="+v+" lock="+lv); } if (bad.length) { console.log(bad.join("\n")); process.exit(1); }' "$FRONTEND_PACKAGE_DIR"
```

---

## Dependency Details

### Core Libraries
- **bootstrap**: UI framework for layout and styling.
- **bootstrap-icons**: Official icon set for Bootstrap.
- **@popperjs/core**: Positioning engine for tooltips and popovers (required by Bootstrap).
- **htmx.org**: Library for modern HTML-first interactivity.

### Validation & Utilities
- **aspnet-client-validation**: ASP.NET Core-compatible client-side validation from `data-val-*` attributes.
- **onmount**: Lifecycle management for DOM elements (used by standard RSJS pattern).
- **react/react-dom**: Mounted island components for richer interactive surfaces.
- **tailwindcss**: Utility styling for the React bundle.

CI restores dependencies with `npm ci --ignore-scripts` before audit/build. Under `AGENTS.md`, package build/test commands are reference commands unless the user explicitly allows running them.

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
