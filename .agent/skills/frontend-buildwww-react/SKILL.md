---
name: frontend-buildwww-react
description: Building and integrating React 19 mounted islands — Islands Architecture, Shadow DOM isolation with Tailwind CSS 4, typed component registry, drnOnmount lifecycle, mount/update/dispose API, Razor page integration patterns, and Vite IIFE build. Keywords: react, islands-architecture, drnOnmount, components, shadow-dom, typescript, tailwind, mount-api, iife
last-updated: 2026-04-16
difficulty: advanced
tokens: ~4K
---

# React Mounted Islands (buildwww)

> React 19 components rendered client-side into isolated Shadow DOM islands within server-rendered Razor Pages — no SPA, no hydration.

## When to Apply
- Creating new React components for Razor Pages
- Modifying `reactBundle.tsx` or the component registry
- Adding island→host communication (callbacks, props updates)
- Debugging Shadow DOM style isolation or lifecycle issues
- Configuring Vite React build (`build:react`)

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [File Layout](#file-layout)
3. [Type System](#type-system)
4. [Vite Build Configuration](#vite-build-configuration)
5. [CSS Strategy](#css-strategy)
6. [The Component Registry](#the-component-registry)
7. [Mount API](#mount-api)
8. [Razor Page Integration](#razor-page-integration)
9. [Adding a New Component](#adding-a-new-component)
10. [Conventions](#conventions)
11. [Related Skills](#related-skills)

---

## Architecture Overview

DRN uses **Mounted Islands Architecture** — React components are explicitly mounted into target DOM nodes as isolated islands, not a monolithic SPA. Each island:

- Renders **client-side only** (no SSR/hydration — Razor Pages handle server rendering)
- Mounts inside **Shadow DOM** by default for CSS isolation from Bootstrap
- Uses **Tailwind CSS 4** inside Shadow DOM while the host page uses Bootstrap 5
- Integrates with **drnOnmount** for lifecycle management (mount on connect, dispose on HTMX swap)
- Communicates with the host page via **typed props and callbacks**

### Script Loading Prerequisites

`reactBundle.tsx` relies on the page-wide script loading order defined in [frontend-buildwww-libraries](../frontend-buildwww-libraries/SKILL.md#script-loading-order). Key constraint: `appPreload.js` **must** initialize `window.DRN.React = {}` before this bundle loads — otherwise `mount()` is not attached and a critical error is logged.

---

## File Layout

```
Sample.Hosted/buildwww/
├── lib/react/
│   ├── reactBundle.tsx          # Entry point — registry, mount API, Shadow DOM, ErrorBoundary
│   ├── reactBundle.css          # CSS aggregator — @reference tailwind, @import components
│   └── components/              # One file pair per component (add more here)
│       ├── HelloReactComponent.tsx   # Reference implementation
│       └── HelloReactComponent.css   # Scoped styles (.drn-react-root prefix)
├── types/
│   └── DrnReactTypes.ts         # Shared types — registry, RootData, ReactMountedIsland
└── app/js/drn/
    └── drnOnmount.js            # Mount/unmount lifecycle manager
```

> `HelloReactComponent` is the **reference implementation**. New components follow the same file-pair pattern inside `components/` and are wired via the 4-point registration described in [Adding a New Component](#adding-a-new-component).

**Output**: `wwwroot/lib/react/reactBundle.[hash].js` + `reactBundle.[hash].css`

---

## Type System

All React-specific types live in `buildwww/types/DrnReactTypes.ts`:

```typescript
// Maps component names to their React types — used by mount() for type-safe props
// Add one entry per component (HelloReact is the reference example)
export type ReactComponentRegistry = {
    'HelloReact': React.ComponentType<HelloReactProps>;
    // 'MyComponent': React.ComponentType<MyComponentProps>;  ← extend here
};

// Tracks mounted roots per DOM element (WeakMap values)
export type RootData<P = unknown> = {
    root: Root;
    name: string;
    isShadow: boolean;
    currentProps?: P;
};

// Cleanup contract for island disposal
export interface Disposable {
    dispose: () => void;
}

// Return type of DRN.React.mount() — enables bidirectional communication
export interface ReactMountedIsland<P> extends Disposable {
    update: (newProps: Partial<P>) => void;
    getProps: () => P | null;
}

// Shadow DOM toggle (default: true)
export interface ReactMountOptions {
    useShadow?: boolean;  // @default true
}
```

---

## Vite Build Configuration

React has its own **dedicated build** invoked via `npm run build:react`:

```javascript
// vite.config.js — react build type
react: {
    plugins: [
        react(),            // @vitejs/plugin-react — JSX transform
        tailwindcss()       // @tailwindcss/vite — Tailwind 4 compilation
    ],
    build: {
        outDir: 'wwwroot/lib/react',
        rolldownOptions: {
            input: {
                reactBundle: resolve(__dirname, 'buildwww/lib/react/reactBundle.tsx'),
            },
            output: {
                format: 'iife',                    // Self-contained, no module imports
                name: 'DrnReactMicroFrontend'      // Global wrapper name
            },
            transform: {
                define: { 'import.meta': '{}' }    // IIFE doesn't support import.meta
            }
        }
    }
}
```

**Key decisions**:
- **IIFE format** — self-executing bundle, no ES module dependency resolution at runtime
- **Separate build** — React bundle is independent from the app/bootstrap/htmx builds
- **Tailwind plugin** — compiles `@apply` directives at build time; no runtime Tailwind in browser

---

## CSS Strategy

### Dual-Import Pattern

`reactBundle.tsx` imports CSS twice for two different purposes:

```typescript
import './reactBundle.css'                    // Extracted to reactBundle.[hash].css by Vite
import bundleStyles from './reactBundle.css?inline';  // Inlined as string for Shadow DOM injection
```

- First import: standard CSS extraction for the build output
- Second import (`?inline`): raw CSS string used to create constructable stylesheets for Shadow DOM

### CSS Aggregation

`reactBundle.css` aggregates all component styles — add one `@import` per component:

```css
@reference "tailwindcss/theme";       /* Load Tailwind theme for @apply resolution */
@reference "tailwindcss/utilities";   /* Load utilities — no global CSS emitted */

@import "./components/HelloReactComponent.css";  /* Reference component */
/* @import "./components/MyComponent.css";          ← add new components here */
```

`@reference` loads Tailwind tokens without emitting `:root` custom properties — prevents leaking into the host page.

### Scoping Convention

All component CSS is scoped under `.drn-react-root`:

```css
.drn-react-root .hello-react { ... }
.drn-react-root .hello-react-card { ... }
```

`.drn-react-root` is applied to:
- **Shadow DOM**: the portal host `<div>` inside the shadow root
- **Light DOM** (`useShadow: false`): added as a class to the mount element

### Shadow DOM Style Injection

```typescript
// Constructable stylesheet (modern browsers)
const drnSharedSheet = new CSSStyleSheet();
drnSharedSheet.replaceSync(bundleStyles);
shadow.adoptedStyleSheets = [...shadow.adoptedStyleSheets, drnSharedSheet];

// Fallback (<style> tag for older browsers)
const styleTag = document.createElement('style');
styleTag.textContent = bundleStyles;
shadow.appendChild(styleTag);
```

### CSS↔JS Coordination (Timing Tokens)

CSS custom properties serve as the single source of truth for animation timing. `HelloReactComponent` demonstrates this pattern:

```css
.drn-react-root .hello-react {
    --badge-fade-ms: 400;
    --title-cycle-ms: 4000;
}
```

JavaScript reads these via `getComputedStyle`:

```javascript
const styles = getComputedStyle(helloRoot);
const FADE_DURATION_MS = parseInt(styles.getPropertyValue('--badge-fade-ms') || '400', 10);
```

> This pattern is optional per component — use it when CSS should own animation timing constants.

---

## The Component Registry

All mountable components are registered in `reactBundle.tsx`. Each component gets one entry — the registry grows as you add components:

```typescript
const COMPONENT_REGISTRY: ReactComponentRegistry = {
    'HelloReact': HelloReactComponent
    // 'MyComponent': MyComponent  ← add new components here
};
```

The registry maps string keys to component types. `DRN.React.mount('HelloReact', ...)` looks up this registry. An unregistered name logs an error listing available components.

### Root Tracking

A `WeakMap<HTMLElement, RootData>` (`rootMap`) tracks mounted roots per DOM element:
- Prevents double-mounting
- Enables safe re-mount (cleans up existing root if component name or shadow mode changes)
- WeakMap ensures garbage collection when elements are removed from DOM

---

## Mount API

### `DRN.React.mount(name, element, props, options?)`

Returns `ReactMountedIsland<P> | null`:

```typescript
const island = DRN.React.mount('HelloReact', domElement, {
    title: 'Hello',
    onReady: () => console.log('Ready'),
    onCardClick: () => console.log('Clicked')
});
```

**Return value API**:

| Method | Purpose |
|--------|---------|
| `island.update(partialProps)` | Merge new props and re-render without remounting |
| `island.getProps()` | Returns shallow copy of current merged props, or `null` after dispose |
| `island.dispose()` | Unmounts React root and removes from tracking map |

### Rendering Pipeline

Every mounted component is wrapped in:

```tsx
<React.StrictMode>
    <IslandErrorBoundary>
        {React.createElement(Component, props)}
    </IslandErrorBoundary>
</React.StrictMode>
```

`IslandErrorBoundary` catches render failures per-island — one crashed island does **not** affect other islands or the host page.

---

## Razor Page Integration

Islands mount via `drnOnmount` — **not** via declarative `data-drn-island` attributes.

### Pattern

```html
<!-- Razor Page (.cshtml) -->
<div data-js-my-component-island></div>

@section Scripts {
    <script>
        DRN.Onmount.registerFull('[data-js-my-component-island]', function (options) {
            const island = DRN.React.mount('MyComponent', this, {
                title: 'Hello',
                onReady: () => console.log('Component ready')
            });

            // Store island as disposable for automatic cleanup on unmount
            options.disposable = island;
        }, function (options) {
            // Custom cleanup (intervals, event listeners, etc.)
            if (this._myIntervalId)
                clearInterval(this._myIntervalId);

            // Call default unregister to dispose 'options.disposable'
            DRN.Onmount.unregister.call(this, options);
        });
    </script>
}
```

### Key Integration Points

1. **`this`** — the DOM element matched by the selector
2. **`options.disposable = island`** — wires island disposal into drnOnmount's unregister lifecycle
3. **Custom unregister** — clean up non-React resources first (intervals, DOM listeners), then delegate to default `unregister`
4. **Shadow DOM access** — `this.shadowRoot` gives access to the shadow root for querying rendered content
5. **Props from server** — use `@Json.Serialize()` to pass server-side data as initial props

### Server-Side Version Injection

```csharp
// Razor Page — pass package versions from PackageVersions.cs
var pkg = PackageVersions.Instance;
// In <script>:
versions: {
    dotnet: @Json.Serialize(pkg.Dotnet),
    react: @Json.Serialize(pkg.React)
}
```

---

## Adding a New Component

### 1. Create component file

```
buildwww/lib/react/components/MyComponent.tsx
buildwww/lib/react/components/MyComponent.css   (optional)
```

### 2. Export props interface and component

```typescript
export interface MyComponentProps {
    title: string;
    onReady?: () => void;  // Follow on* callback convention
}

export const MyComponent = ({ title, onReady }: MyComponentProps) => {
    useEffect(() => { onReady?.(); }, [onReady]);
    return <div className="my-component">{title}</div>;
};
```

### 3. Register in type system

Update `buildwww/types/DrnReactTypes.ts`:

```typescript
export type ReactComponentRegistry = {
    'HelloReact': React.ComponentType<HelloReactProps>;
    'MyComponent': React.ComponentType<MyComponentProps>;  // ← Add
};
```

### 4. Register in bundle

Update `reactBundle.tsx`:

```typescript
import { MyComponent } from './components/MyComponent';

const COMPONENT_REGISTRY: ReactComponentRegistry = {
    'HelloReact': HelloReactComponent,
    'MyComponent': MyComponent  // ← Add
};
```

### 5. Add CSS import (if applicable)

Update `reactBundle.css`:

```css
@import "./components/MyComponent.css";
```

### 6. Mount from Razor

Follow the [Razor Page Integration](#razor-page-integration) pattern.

### 7. Build

```bash
npm run build:react   # BUILD_TYPE=react vite build
```

---

## Conventions

### Callback Convention

Components accept `on*`-prefixed function props for island→host event notification:

| Rule | Example |
|------|---------|
| Name with `on` prefix | `onReady`, `onSelectionChange`, `onSubmit` |
| Execute in host page scope | Outside Shadow DOM — vanilla JS context |
| Replaceable via `update()` | `island.update({ onReady: newHandler })` |
| Removable via `update()` | `island.update({ onReady: undefined })` |
| Pass plain data only | No React internals in callback arguments |

### CSS Conventions

- All selectors scoped under `.drn-react-root .component-name`
- Use `@apply` from Tailwind utilities — compiles at build time
- Timing tokens as CSS custom properties (CSS is source of truth)
- No global `:root` styles — use `@reference` not `@import` for Tailwind

### Lifecycle Conventions

- Always set `options.disposable = island` in `registerFull`
- Custom cleanup (intervals, DOM listeners) in unregister callback **before** `DRN.Onmount.unregister.call()`
- Never call `root.unmount()` directly — use `island.dispose()` instead

### Evolution Triggers (from HomeAnonymous.cshtml roadmap)

| Trigger | Action |
|---------|--------|
| 5–6+ components with shared state | Add lightweight cross-island state bus |
| Bundle size concern | Lazy-load registry entries with dynamic `import()` |
| High-performance isolated islands | Consider Svelte 5 or Vue Vapor mode registries |
| Multiple frameworks coexist | Invest in full micro-frontend separation |

---

## Related Skills

- [frontend-buildwww-libraries](../frontend-buildwww-libraries/SKILL.md) — `drnOnmount` structure and lifecycle
- [frontend-buildwww-vite](../frontend-buildwww-vite/SKILL.md) — Vite multi-build configuration
- [frontend-buildwww-packages](../frontend-buildwww-packages/SKILL.md) — React, Tailwind, and type dependency management
