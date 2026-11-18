import { defineConfig } from 'vitest/config';

export const unitTestPattern = 'buildwww/tests/**/*.test.{js,ts}';
export const defaultCoverageIncludes = ['buildwww/{app,lib}/**/*.{ts,tsx,js}'];
export const defaultCoverageExcludes = ['**/*.d.ts'];

export default defineConfig({
    test: {
        include: [unitTestPattern],
        restoreMocks: true,
        unstubEnvs: true,
        reporters: ['default', './buildwww/tests/reporters/vitest.ts'],
        coverage: {
            provider: 'v8',
            reporter: ['lcovonly'],
            include: defaultCoverageIncludes,
            exclude: defaultCoverageExcludes,
        },
    },
});
