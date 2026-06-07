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

## Package Configuration

This acts as the baseline `package.json` for frontend projects.

```json
{
  "name": "sample.hosted",
  "private": true,
  "version": "0.0.1",
  "scripts": {
    "test": "echo \"Error: no test specified\" && exit 1",
    "build": "npm run build:app && npm run build:appPostload && npm run build:htmx && npm run build:bootstrap && npm run build:react",
    "build:app": "BUILD_TYPE=app vite build",
    "build:appPostload": "BUILD_TYPE=appPostload vite build",
    "build:htmx": "BUILD_TYPE=htmx vite build",
    "build:bootstrap": "BUILD_TYPE=bootstrap vite build",
    "build:react": "BUILD_TYPE=react vite build"
  },
  "type": "module",
  "dependencies": {
    "@popperjs/core": "^2.11.8",
    "aspnet-client-validation": "^0.11.1",
    "bootstrap": "^5.3.8",
    "bootstrap-icons": "^1.13.1",
    "htmx.org": "^2.0.10",
    "onmount": "^2.0.0",
    "react": "^19.2.6",
    "react-dom": "^19.2.6",
    "tailwindcss": "^4.3.0"
  },
  "devDependencies": {
    "@tailwindcss/vite": "^4.3.0",
    "@types/react": "^19.2.15",
    "@types/react-dom": "^19.2.3",
    "@vitejs/plugin-react": "^6.0.2",
    "globals": "^17.6.0",
    "sass": "1.100.0",
    "typescript": "^6.0.3",
    "vite": "^8.0.14"
  }
}
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
