import { defineConfig } from 'vitest/config';

export default defineConfig({
    test: {
        include: ['buildwww/tests/**/*.test.{js,ts}'],
        restoreMocks: true,
        unstubEnvs: true,
        reporters: ['default', './buildwww/tests/reporters/vitest.ts'],
        coverage: {
            provider: 'v8',
            reporter: ['lcovonly'],
            include: ['buildwww/{app,lib}/**/*.{ts,tsx,js}'],
            exclude: ['**/*.d.ts'],
        },
    },
});
