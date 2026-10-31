import { defineConfig } from '@playwright/test';

export default defineConfig({
    testDir: './buildwww/tests/browser',
    testMatch: '**/*.spec.js',
    forbidOnly: !!process.env.CI,
    workers: 1,
    retries: 0,
    timeout: 30_000,
    reporter: 'list',
    use: {
        browserName: 'chromium',
        baseURL: 'http://127.0.0.1:4175',
        headless: true,
        trace: 'retain-on-failure'
    },
    webServer: {
        command: 'npm run test:browser:serve',
        url: 'http://127.0.0.1:4175',
        reuseExistingServer: false,
        timeout: 30_000
    }
});
