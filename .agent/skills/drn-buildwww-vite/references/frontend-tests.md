# Frontend Test Organization

- Group unit tests under `buildwww/tests/unit/<module>/`, such as `unit/drn/`. Keep JavaScript and TypeScript suites together.
- Tests of TypeScript frontend code must use `.test.ts` or Playwright `.spec.ts`. Migrate existing JavaScript suites when their owning area is reviewed.
- The package manifest owns commands. Use quoted recursive globs to discover `.test.js` and `.test.ts` under `buildwww/tests/`.
- Keep Playwright `.spec.js` and `.spec.ts` suites under `buildwww/tests/browser/`. Run them separately after unit tests pass.
- When moving tests, update imports, fixture paths, discovery, and documentation.

## TypeScript Test Checks

- Node execution does not type-check tests. Include TypeScript tests in a test configuration with Node types, reuse applicable application compiler options, and include it in the package's type-check command.
- Explain deliberate invalid inputs with scoped `@ts-expect-error` comments.
- Include extensions in relative runtime imports. TypeScript aliases do not configure Node resolution. Use `import type` for types.
- Tests and type checks require repository execution permission.
