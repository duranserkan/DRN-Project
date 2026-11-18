# Frontend Test Organization

- Put new unit tests in `buildwww/tests/unit/<module>/` and name them `*.test.ts`.
- The package manifest owns commands. Use quoted recursive globs to discover `.test.js` and `.test.ts` under `buildwww/tests/`.
- Exercise new production TSX in the Vitest unit suite so source lines appear in LCOV. The Sample frontend guide owns coverage scope, CI summaries, and Sonar quality gate requirements.
- Put Vite fixture specs in `buildwww/tests/browser/` and specs that need a running Sample app in `buildwww/tests/hosted/`. Use `*.spec.ts` for both and TypeScript for their fixtures. Keep fixture specs isolated so they can run in parallel with each other and the unit suite.
- When moving tests, update imports, fixture paths, discovery, and documentation.

## TypeScript Test Checks

- Vitest transforms TypeScript during the unit run. Include the tests and Vitest config in the package's type-check command, using the test configuration with Node types and shared application compiler options.
- Explain deliberate invalid inputs with scoped `@ts-expect-error` comments.
- Follow the [buildwww import convention](type-ownership.md) for local runtime dependencies in tests and their Node entry points.
- Tests and type checks require repository execution permission.
