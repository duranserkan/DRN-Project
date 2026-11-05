# Type Ownership and Declaration Files

- Write new frontend scripting in TypeScript whenever the tooling supports it. Migrate JavaScript as its owning area is reviewed, keeping exceptions where tooling requires JavaScript.
- Prefer `buildwww/types/*.types.ts` for shared browser contracts. Small module-owned types may stay in the implementation file. Use `import type` for type-only consumers.
- Reserve `.d.ts` for ambient declarations and untyped JavaScript APIs. Generate declarations for published libraries.
- Keep `window.DRN` in `global.d.ts`, importing shared contracts. Retain interfaces that define the public API and check implementations against them.
- Existing `.d.ts` contracts are valid. Migrate them when reviewing their owning modules, updating imports and documentation before removing obsolete files. Avoid bulk renames.
