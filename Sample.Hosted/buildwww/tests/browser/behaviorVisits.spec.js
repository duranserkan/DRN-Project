import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';

// Exercise the shipped classic scripts: ES-module fixtures would execute imports only once.
// Rebuild affected frontend assets before running this suite after source changes.
const webRoot = new URL('../../../wwwroot/', import.meta.url);
const assets = new Map();
function bundle(directory, source) {
    const manifest = JSON.parse(readFileSync(new URL(`${directory}/.vite/manifest.json`, webRoot), 'utf8'));
    const file = manifest[source].file;
    const body = readFileSync(new URL(`${directory}/${file}`, webRoot));
    const url = `/behavior-assets/${directory}/${file}`;
    assets.set(url, body);
    const integrity = `sha256-${createHash('sha256').update(body).digest('base64')}`;
    return nonce => `<script nonce="${nonce}" src="${url}" integrity="${integrity}"></script>`;
}

const runtime = [
    bundle('app', 'buildwww/app/js/appPreload.js'),
    bundle('lib/htmx', 'buildwww/lib/htmx/htmxBundle.js'),
    bundle('lib/bootstrap', 'buildwww/lib/bootstrap/bootstrapBundle.js'),
    bundle('appPostload', 'buildwww/app/js/appPostload.js'),
    bundle('lib/react', 'buildwww/lib/react/reactBundle.tsx'),
    bundle('behaviors/layout', 'buildwww/app/js/layoutBehaviors.js')
];
const home = bundle('behaviors/home', 'buildwww/app/js/homeBehaviors.js');
const demo = bundle('behaviors/test', 'buildwww/app/js/testBehaviors.js');
const intentionalError = 'Intentional unhandled browser exception from the HTMX test page.';

function documentFor(area, nonce, partial) {
    const content = area === 'home'
        ? '<div data-js-hello-react-island data-versions="{}"></div>'
        : area === 'test'
            ? '<button data-js-unhandled-browser-exception>Trigger browser exception</button>'
            : '<p>No page behaviors needed</p>';
    const pageScript = area === 'home' ? home(nonce) : area === 'test' ? demo(nonce) : '';
    return `<!doctype html><html><head>
        <meta name="drn-app-base" content="/">
        <meta name="htmx-config" content='{"inlineScriptNonce":"${nonce}","inlineStyleNonce":"${nonce}","defaultSettleDelay":0}'>
        ${partial ? '' : runtime.map(script => script(nonce)).join('\n')}
        </head><body hx-boost="true" hx-ext="safe-nonce" hx-history="false">
        <nav><a href="/behavior-pages/home">Home</a>
        <a href="/behavior-pages/test">Test</a>
        <a href="/behavior-pages/away">Away</a></nav>
        <main data-page="${area}">${content}</main>${pageScript}</body></html>`;
}

test.beforeEach(async ({ page }) => {
    await page.route('**/behavior-assets/**', route => {
        const body = assets.get(new URL(route.request().url()).pathname);
        return route.fulfill({ status: body ? 200 : 404, contentType: 'text/javascript', body: body || '' });
    });
    let responseNumber = 0;
    await page.route('**/behavior-pages/*', route => {
        const area = new URL(route.request().url()).pathname.split('/').at(-1);
        const nonce = `behavior-response-${++responseNumber}`;
        const partial = route.request().headers()['hx-request'] === 'true';
        return route.fulfill({
            contentType: 'text/html',
            headers: {
                'HX-Nonce': nonce,
                'Content-Security-Policy': `default-src 'self'; script-src 'self' 'nonce-${nonce}'; style-src 'self' 'nonce-${nonce}'`
            },
            body: documentFor(area, nonce, partial)
        });
    });
});

async function visit(page, area) {
    const response = page.waitForResponse(`/behavior-pages/${area}`);
    await page.getByRole('link', { name: { home: 'Home', test: 'Test', away: 'Away' }[area], exact: true }).click();
    expect((await response).request().headers()['hx-request']).toBe('true');
    await expect(page.locator('main')).toHaveAttribute('data-page', area);
}

test('repeated test-page visits execute the bundle but attach only one disposable click handler', async ({ page }) => {
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto('/behavior-pages/away');
    await page.evaluate(() => {
        window.behaviorRegistrations = 0;
        const register = DRN.Onmount.register;
        DRN.Onmount.register = function (...args) {
            if (args[0] === '[data-js-unhandled-browser-exception]') window.behaviorRegistrations++;
            return register.apply(this, args);
        };
    });

    for (let visitNumber = 1; visitNumber <= 4; visitNumber++) {
        await visit(page, 'test');
        await expect.poll(() => page.evaluate(() => window.behaviorRegistrations)).toBe(visitNumber);
        const button = page.getByRole('button', { name: 'Trigger browser exception' });
        const previousButton = await button.elementHandle();
        await button.click();
        expect(errors).toEqual(Array(visitNumber).fill(intentionalError));

        await visit(page, 'away');
        // A retained detached node must no longer invoke its former handler.
        await previousButton.evaluate(element => element.click());
        expect(errors).toEqual(Array(visitNumber).fill(intentionalError));
        await previousButton.dispose();
    }
});

test('repeated home visits dispose React roots, intervals, and in-flight title timeouts', async ({ page }) => {
    const clicks = [];
    const errors = [];
    page.on('console', message => {
        if (message.text().startsWith('Card clicked!')) clicks.push(message.text());
    });
    page.on('pageerror', error => errors.push(error.message));
    await page.goto('/behavior-pages/away');
    await page.clock.install();
    await page.clock.pauseAt(Date.now() + 1000);
    await page.evaluate(() => {
        const audit = window.homeTimers = { intervals: new Set(), timeouts: new Set(), ticks: 0, transitions: 0 };
        // Observe actual timer allocation/cancellation; do not replace lifecycle callbacks.
        for (const [method, collection, counter] of [
            ['setInterval', 'intervals', 'ticks'], ['setTimeout', 'timeouts', 'transitions']
        ]) {
            const original = window[method].bind(window);
            window[method] = (callback, delay, ...args) => {
                if (!new Error().stack.includes('/behavior-assets/behaviors/home/'))
                    return original(callback, delay, ...args);
                const id = original(() => {
                    if (collection === 'timeouts') audit[collection].delete(id);
                    audit[counter]++;
                    callback(...args);
                }, delay);
                audit[collection].add(id);
                return id;
            };
        }
        for (const method of ['clearInterval', 'clearTimeout']) {
            const original = window[method].bind(window);
            window[method] = id => {
                audit.intervals.delete(id);
                audit.timeouts.delete(id);
                return original(id);
            };
        }
    });

    for (let visitNumber = 1; visitNumber <= 3; visitNumber++) {
        await visit(page, 'home');
        const island = page.locator('[data-js-hello-react-island]');
        await expect(island.locator('.hello-react')).toBeVisible();
        await expect.poll(() => page.evaluate(() => window.homeTimers.intervals.size)).toBe(1);
        await island.getByRole('button').first().click();
        expect(clicks).toEqual(Array(visitNumber).fill('Card clicked! Total clicks outside the component: 1'));
        const oldIsland = await island.elementHandle();

        // Leave during the fade, when both the recurring timer and a timeout exist.
        await page.clock.runFor(4000);
        expect(await page.evaluate(() => ({
            intervals: homeTimers.intervals.size, timeouts: homeTimers.timeouts.size,
            ticks: homeTimers.ticks, transitions: homeTimers.transitions
        }))).toEqual({ intervals: 1, timeouts: 1, ticks: visitNumber, transitions: 0 });
        await visit(page, 'away');
        await expect.poll(() => page.evaluate(() => [homeTimers.intervals.size, homeTimers.timeouts.size]))
            .toEqual([0, 0]);
        expect(await oldIsland.evaluate(element => element.shadowRoot.querySelector('.hello-react'))).toBeNull();
        await oldIsland.dispose();

        await page.clock.runFor(8000);
        expect(await page.evaluate(() => [homeTimers.ticks, homeTimers.transitions])).toEqual([visitNumber, 0]);
    }
    expect(errors).toEqual([]);
});
