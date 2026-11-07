import { defineConfig } from '@playwright/test';

export default defineConfig({
    testDir: '.',
    testMatch: '**/*.spec.ts',
    forbidOnly: !!process.env.CI,
    use: {
        baseURL: process.env.SAMPLE_HOSTED_URL ?? 'http://localhost:5998/',
        trace: 'retain-on-failure',
    },
});
