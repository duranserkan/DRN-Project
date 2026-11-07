# Type Ownership and Declaration Files

## Source language

Use TypeScript for new project-owned frontend source, tests, fixtures, and tooling configuration that accepts executable code. Use `.tsx` for JSX. Include new files in the appropriate compiler project.

Use JavaScript only when a tool or runtime requires it, or when a fixture specifically exercises JavaScript behavior. Record the concrete reason in the change description. Apply the same rule when migrating existing JavaScript in the area under review. Generated bundles and vendored files are outside this policy.

## Local import paths

| Reference | Convention | Example |
|---|---|---|
| Type from a sibling module | Use an extensionless relative path. | `import type { DrnNotification } from './drnNotifications.types'` |
| Type from another directory under `buildwww/app/ts/` | Use the extensionless `@ts/` alias. | `typeof import('@ts/drn/drnCookieManager')` |
| Shared contract in `buildwww/types/` from outside that directory | Use extensionless `@/types/`. | `import type { IDrnDiagnostics } from '@/types/drnDiagnostics.types'` |
| Local runtime dependency | Use a relative path with the actual file extension so direct Node tests can resolve it. | `import drnUtils from './drnUtils.ts'` |

Apply the type path rules to `import type`, type-only re-exports, and type queries. Apply the runtime rule to value re-exports, side-effect imports, and dynamic imports.

## Type ownership

- Prefer `buildwww/types/*.types.ts` for shared browser contracts. Small module-owned types may stay in the implementation file. Use `import type` for type-only consumers.
- Keep the `I` prefix for service and browser API contracts, such as `IDrnProblem` and `IDrnReact`. Use plain names for data shapes and options, such as `DrnProblem` and `ReactMountOptions`, even when declared with `interface`.
- Reserve `.d.ts` for ambient declarations and untyped JavaScript APIs. Generate declarations for published libraries.
- Keep `window.DRN` in `global.d.ts`, importing shared contracts. Retain interfaces that define the public API and check implementations against them.
- Existing `.d.ts` contracts are valid. Migrate them when reviewing their owning modules, updating imports and documentation before removing obsolete files. Avoid bulk renames.
