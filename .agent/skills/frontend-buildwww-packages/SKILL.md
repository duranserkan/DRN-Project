---
name: frontend-buildwww-packages
description: Frontend package dependencies - Standardized npm packages (Bootstrap, htmx, jQuery validation, onmount), version management, and dependency purposes. Reference for adding or updating frontend libraries. Keywords: npm, packages, dependencies, bootstrap, htmx, jquery, onmount, package-management, versioning
last-updated: 2026-02-15
difficulty: basic
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
    "build:app": "BUILD_TYPE=app vite build",
    "build:htmx": "BUILD_TYPE=htmx vite build",
    "build:bootstrap": "BUILD_TYPE=bootstrap vite build"
  },
  "type": "module",
  "dependencies": {
    "bootstrap": "^5.3.8",
    "bootstrap-icons": "^1.13.1",
    "@popperjs/core": "^2.11.8",
    "htmx.org": "^2.0.8",
    "jquery": "^3.7.1",
    "jquery-validation": "^1.21.0",
    "jquery-validation-unobtrusive": "^4.0.0",
    "onmount": "^2.0.0"
  },
  "devDependencies": {
    "globals": "^16.5.0",
    "sass": "1.94.3",
    "typescript": "^5.9.3",
    "vite": "^7.3.1"
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
- **jquery**: Required dependency for validation libraries.
- **jquery-validation**: Client-side form validation.
- **jquery-validation-unobtrusive**: ASP.NET Core integration for unobtrusive validation attributes.
- **onmount**: Lifecycle management for DOM elements (used by standard RSJS pattern).

### Build & Dev Tooling
- **vite**: Next Generation Frontend Tooling.
- **sass**: CSS preprocessor.
- **typescript**: Typed JavaScript superset.
- **globals**: Global variable definitions (often used for linting contexts).

---

## Related Skills

- [frontend-buildwww-vite.md](../frontend-buildwww-vite/SKILL.md) - Build process configuration
- [frontend-buildwww-libraries.md](../frontend-buildwww-libraries/SKILL.md) - Library implementation and usage patterns
