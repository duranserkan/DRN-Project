---
name: drn-buildwww-react
description: "DRN React mounted islands. Keywords: drn, buildwww, react, shadow-dom, tailwind, typescript, onmount, mount-api, iife"
last-updated: 2026-11-13
difficulty: advanced
---

# DRN React Mounted Islands

Use when the profile declares DRN buildwww and the task concerns React components, typed registries, host callbacks, Shadow DOM, styles, or mounting lifecycle. Keep `AGENTS.md`, DiSCOS, and the profile active. Resolve dependency versions from manifests and lockfiles.

## Contents

- [Architecture Overview](#architecture-overview)
- [File Layout](#file-layout)
- [Type System](#type-system)
- [Vite Build Configuration](#vite-build-configuration)
- [CSS Strategy](#css-strategy)
- [The Component Registry](#the-component-registry)
- [Mount API](#mount-api)
- [Razor Page Integration](#razor-page-integration)
- [Adding a New Component](#adding-a-new-component)
- [Conventions](#conventions)

## Architecture Overview

Razor renders the host HTML. React renders client-side islands, without SSR or hydration. Islands use Shadow DOM by default, Tailwind inside the island, Bootstrap outside, typed props/callbacks for communication, and `DRN.Onmount` for activation and disposal on navigation.

### Script Loading Prerequisites

Preload provides `DRN.React.mount`. It returns `null` until the React bundle installs its implementation. React stays global in the current shell. Only home mounting behavior is page-specific. See [loading order](../drn-buildwww-libraries/SKILL.md#script-loading-order).

`DRN`, `Drn*`, and `DRN.Framework.*` are shared framework names, not placeholders.

## File Layout

| Owner | Purpose |
|---|---|
| `buildwww/lib/react/reactBundle.tsx` | Registry and mount API |
| `reactBundle.css` | Tailwind references and component CSS imports |
| `components/` | TSX and optional CSS file pairs |
| `PortalContext.tsx` | `IslandPortal` component for overlays |
| `buildwww/types/drnReact.types.ts` | Public mount options, registry, and island handle |
| `buildwww/app/ts/behaviors/home/` | Page-specific island mounting |
| `buildwww/app/ts/drn/drnOnmount.ts` | Lifecycle registration wrapper |

HelloReact is the reference component. Resolve output paths from Vite's manifest, not copied hashes. Keep implementation details in these owners instead of duplicating them in skills.

## Type System

`ReactComponentRegistry` maps names to component types and their props. Keep it aligned with runtime `COMPONENT_REGISTRY`. Use the shared public types rather than parallel definitions. `ReactMountOptions.useShadow` defaults to true. See [Mount API](#mount-api) for the returned handle.

## Vite Build Configuration

The dedicated `build:react` target uses React and Tailwind plugins. A single JavaScript entry permits native IIFE output named `DrnReactMicroFrontend`, independent of runtime/behavior builds. Preserve the existing `import.meta` transformation for classic-script output. Mixed-entry classic builds use [Vite isolation guidance](../drn-buildwww-vite/SKILL.md#vite-configuration).

Building requires explicit execution permission. A home-behavior-only change uses `build:behaviors:home`. Component, registry, or React CSS changes require the React build too. Markup/configuration changes also require deploying the Razor view.

## CSS Strategy

### CSS Aggregation

`reactBundle.tsx` imports `reactBundle.css?inline` for stylesheet injection. The aggregator imports component styles and uses Tailwind `@reference` so tokens support `@apply` without emitting global rules. Do not add a second emitted stylesheet unless the output contract needs one.

### Scoping Convention

Scope document-visible selectors under `.drn-react-root`, including resets. The shared sheet is also adopted into the document for light-DOM islands, so Shadow DOM alone does not isolate it. Reserve `:host` for Shadow DOM behavior. Avoid global `:root` and document-wide `*` resets.

Both shadow and light-DOM mounts receive `.drn-react-root`. Component CSS must also work in the shared portal's Shadow DOM.

### Shadow DOM Style Injection

Preserve styles in document, island, and portal roots. Reuse existing style/nonce helpers. Fallback `<style>` elements must carry the response nonce. Never replace another component's adopted sheets.

### CSS and JavaScript Timing

CSS custom properties may own animation timing. Preserve fallback durations and clear every interval or pending timeout on unmount.

## The Component Registry

Register components in both `ReactComponentRegistry` and runtime `COMPONENT_REGISTRY`. Unknown names log an error and return null. Repeated mounting must not duplicate roots. Changing the component or shadow mode replaces the previous island.

## Mount API

### `DRN.React.mount(name, element, props, options?)`

Returns an island handle or null for an invalid element/component. `update(partialProps)` merges props and rerenders, `getProps()` returns a shallow copy or null after disposal, and `dispose()` unmounts. Old handles cannot mutate/dispose a replacement root on the same element.

Each island uses StrictMode. Render failures remain local and can recover when props change.

### Global Portal Host

`IslandPortal` renders overlays into shared Shadow DOM. Keep styles under `.drn-react-root`. New islands must have working portals after htmx navigation. Dispose the owning island to remove its overlays. Never mount portals while htmx removes old body children. A component can render a notice through the portal:

```tsx
import { IslandPortal } from '../PortalContext.tsx';

function SaveNotice({ visible }: { visible: boolean }) {
    return visible ? <IslandPortal><p role="status">Changes saved.</p></IslandPortal> : null;
}
```

## Razor Page Integration

Render declarative data and an external page bundle. Put registration in the domain behavior module, not an inline Razor script. Home references `homeBehaviors.ts` in its `Scripts` section. Full and htmx layouts must render that section.

```razor
@{
    var versions = Json.Serialize(new { dotnet = pkg.Dotnet, react = pkg.React }).ToString();
}
<div data-js-hello-react-island data-versions="@versions"></div>
@section Scripts {
    <script src="buildwww/app/ts/homeBehaviors.ts"></script>
}
```

Here `pkg` is the server package-version provider. Convert JSON to string so Razor encodes attribute quotes. Never use raw HTML for data attributes. Read configuration from the mounted element on each navigation. The home behavior’s `readVersions` parses JSON as `unknown`, checks the version strings, and falls back to component defaults for malformed data.

```typescript
// behaviors/home/hello-react-island.ts, imported by homeBehaviors.ts
DRN.Onmount.register('[data-js-hello-react-island]', function (options) {
    if (!(this instanceof HTMLElement)) return;
    const island = DRN.React.mount('HelloReact', this, {
        title: 'Hello',
        versions: readVersions(this.dataset.versions)
    });
    if (!island) return false;
    options.disposable = island;
});
```

Use the [Onmount lifecycle](../drn-buildwww-libraries/SKILL.md#onmount-wrapper) for repeated registration, late loading, and cleanup. Return `false` when React is not ready so a later poll retries the mount. Dispose extra resources before the island.

Access rendered Shadow DOM through `shadowRoot`. React rendering is asynchronous. Use readiness callbacks when behavior needs rendered descendants.

## Adding a New Component

1. Use the registry key as the component filename and named export, as in `components/HelloReact.tsx` exporting `HelloReact`. Give its optional scoped stylesheet the same basename.
2. Add the key to `ReactComponentRegistry` and register the named export in `COMPONENT_REGISTRY` using property shorthand.
3. Import its CSS from `reactBundle.css` when needed.
4. Add mounting behavior to the owning domain and import it from the domain entry.
5. Render its hook/configuration and page bundle reference. Preserve runtime dependency order.
6. When execution is authorized, build changed runtime/behavior targets and verify full load, htmx entry, revisit, and unmount.

## Conventions

Follow the shared [frontend type naming and ownership guidance](../drn-buildwww-vite/references/type-ownership.md) for React APIs, props, and options.

Follow [UI accessibility guidance](../basic-code-review/SKILL.md#ui-accessibility). React event props do not need inline-script CSP permission. Bundle/style nonce rules still apply.

### Callback Convention

Use `on`-prefixed function props, such as `onReady` and `onCardClick`. Callbacks execute in host scope, can be replaced through `update`, or removed by setting undefined. Pass plain data, not React internals. Keep StrictMode callback handling safe against repeated effects.

### Lifecycle Conventions

- Keep handles/resources local to each mounted element.
- Assign the handle or composite object to `options.disposable`.
- For custom unregister, release timers/listeners before `DRN.Onmount.unregister.call(this, options)`.
- Use `island.dispose()`, not direct `root.unmount()` from host code.
- Full reload adopts new code for existing registrations. Cache reuse does not replace registration.

### Evolution Triggers

Add shared-state or lazy-loading mechanisms only for a proven requirement. Domain behavior bundles alone do not require a new runtime or independent application.

## Related Skills

- [Behavior lifecycle](../drn-buildwww-libraries/SKILL.md)
- [Vite builds](../drn-buildwww-vite/SKILL.md)
- [Package dependencies](../drn-buildwww-packages/SKILL.md)
