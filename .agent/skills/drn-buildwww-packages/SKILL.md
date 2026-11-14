---
name: drn-buildwww-packages
description: "Select and update DRN frontend dependencies. Use for npm versions, lockfiles, package imports, and tooling."
last-updated: 2026-11-13
difficulty: basic
---

# DRN buildwww npm Packages

## When to Apply
- Adding new npm libraries
- Updating existing dependencies
- Checking version compatibility
- Understanding the purpose of installed packages
- Choosing third-party import paths

---

## Source Of Truth

Resolve the frontend root from the repository profile. Otherwise, find the `package.json` that owns Vite, `buildwww/`, `src/`, or frontend scripts. Its manifest owns scripts and pinned top-level versions. Its lockfile owns resolved dependencies. Keep version numbers in those files.

Pin the verified Node.js LTS release for CI and local development. Match the package engine and root lockfile metadata to the exact runtime pin. Keep setup docs aligned and verify the local runtime matches. Keep version numbers in repository configuration, not skills.

Use exact versions in `dependencies` and `devDependencies`. Verify provenance under the AGENTS.md version-update gate before committing.

Inspect manifests safely. Every npm command below, including dry runs and registry lookups, requires the explicit request and confirmation in AGENTS.md. When changing packages:

1. Set `FRONTEND_PACKAGE_DIR` to the absolute path of the discovered package root before changing directories.
2. Edit `$FRONTEND_PACKAGE_DIR/package.json` or run the contributor guide's safeguarded `--save-exact` install command from `"$FRONTEND_PACKAGE_DIR"`.
3. Refresh lock metadata with `npm install --prefix "$FRONTEND_PACKAGE_DIR" --package-lock-only --ignore-scripts --engine-strict --registry=https://registry.npmjs.org/`.
4. Compare both dependency maps in `package.json` with the root lock entry. Preserve exact names and versions. Do not create or run an ad-hoc verifier without the required approval.
5. Run `npm ci --prefix "$FRONTEND_PACKAGE_DIR" --ignore-scripts --engine-strict --registry=https://registry.npmjs.org/ --dry-run` to verify install consistency.

---

## Package Imports

- Use package specifiers and public subpaths for third-party scripts and styles. Verify entry points against package metadata or upstream documentation. Do not depend on private package internals.
- Do not add relative imports that traverse into `node_modules`.
- Existing stylesheet traversals may remain until their replacement is verified with the configured resolver and an authorized frontend build. Treat them as migration work, not examples for new imports.

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

CI restores dependencies with install scripts disabled before audit/build.

### Build & Dev Tooling
- **@axe-core/playwright**: Accessibility scans of rendered fixture states and the hosted Sample home page.
- **vite**: Frontend builds and development server.
- **sass-embedded**: Sass compiler for SCSS.
- **typescript**: Typed JavaScript superset.
- **oxfmt**: Formats frontend sources and tool configs. The aggregate check verifies formatting before linting.
- **stylelint**: Checks authored CSS and SCSS. The frontend package owns the presets and Tailwind directive exceptions.
- **knip**: The aggregate check reports unused files, exports, and dependencies. The standalone `check:unused` command fails on findings. Keep entry points aligned with Vite variants and Razor-loaded bundles.
- **oxlint** and **oxlint-tsgolint**: Type-aware JavaScript and TypeScript linting. The `lint` command fails on warnings and errors. See the frontend root's `.oxlintrc.json` for rules.
- **@tailwindcss/vite** and **@vitejs/plugin-react**: Vite plugins for Tailwind and React builds.

---

## Related Skills

- [drn-buildwww-vite.md](../drn-buildwww-vite/SKILL.md) - Build process configuration
- [drn-buildwww-libraries.md](../drn-buildwww-libraries/SKILL.md) - Library implementation and usage patterns
