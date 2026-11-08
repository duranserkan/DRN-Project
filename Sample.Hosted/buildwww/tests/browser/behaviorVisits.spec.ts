import { test, expect, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { brotliDecompressSync } from 'node:zlib';

// Exercise the shipped classic scripts: ES-module fixtures would execute imports only once.
// Rebuild affected frontend assets before running this suite after source changes.
const webRoot = new URL('../../../wwwroot/', import.meta.url);
type TimerAudit = { intervals: Set<number>; timeouts: Set<number>; ticks: number; transitions: number };
declare global {
    interface Window {
        behaviorRegistrations: number;
    }
    var homeTimers: TimerAudit;
}

const assets = new Map<string, Buffer>();
function bundle(directory: string, source: string) {
    const manifest = JSON.parse(readFileSync(new URL(`${directory}/.vite/manifest.json`, webRoot), 'utf8'));
    const entry = manifest[source];
    const file = entry.brotliOnly ? entry.file + '.br' : entry.file;
    const bytes = readFileSync(new URL(`${directory}/${file}`, webRoot));
    const body = entry.brotliOnly ? brotliDecompressSync(bytes) : bytes;
    const url = `/behavior-assets/${directory}/${file}`;
    assets.set(url, body);
    const integrity = `sha256-${entry.hash}`;
    return (nonce: string) => `<script nonce="${nonce}" src="${url}" integrity="${integrity}"></script>`;
}

const runtime = [
    bundle('app', 'buildwww/app/ts/appPreload.ts'),
    bundle('lib/htmx', 'buildwww/lib/htmx/htmxBundle.ts'),
    bundle('lib/bootstrap', 'buildwww/lib/bootstrap/bootstrapBundle.ts'),
    bundle('appPostload', 'buildwww/app/ts/appPostload.ts'),
    bundle('lib/react', 'buildwww/lib/react/reactBundle.tsx'),
    bundle('behaviors/layout', 'buildwww/app/ts/layoutBehaviors.ts'),
];
const home = bundle('behaviors/home', 'buildwww/app/ts/homeBehaviors.ts');
const demo = bundle('behaviors/test', 'buildwww/app/ts/testBehaviors.ts');
const intentionalError = 'Intentional unhandled browser exception from the HTMX test page.';

function documentFor(
    area: string,
    nonce: string,
    partial: boolean,
    fallbackContent = '<p>No page behaviors needed</p>',
) {
    const content =
        area === 'home'
            ? '<div data-js-hello-react-island data-versions="{}"></div>'
            : area === 'test'
              ? '<button data-js-unhandled-browser-exception>Trigger browser exception</button>'
              : fallbackContent;
    const pageScript = area === 'home' ? home(nonce) : area === 'test' ? demo(nonce) : '';
    return `<!doctype html><html><head>
        <meta name="drn-app-base" content="/">
        <meta name="htmx-config" content='{"inlineScriptNonce":"${nonce}","inlineStyleNonce":"${nonce}","defaultSettleDelay":0}'>
        ${partial ? '' : runtime.map((script) => script(nonce)).join('\n')}
        </head><body hx-boost="true" hx-ext="safe-nonce" hx-history="false">
        <nav><a href="/behavior-pages/home">Home</a>
        <a href="/behavior-pages/test">Test</a>
        <a href="/behavior-pages/away">Away</a></nav>
        <main data-page="${area}">${content}</main>${pageScript}</body></html>`;
}

test.beforeEach(async ({ page }) => {
    await page.route('**/behavior-assets/**', (route) => {
        const body = assets.get(new URL(route.request().url()).pathname);
        return route.fulfill({ status: body ? 200 : 404, contentType: 'text/javascript', body: body || '' });
    });
    let responseNumber = 0;
    await page.route('**/behavior-pages/*', (route) => {
        const area = new URL(route.request().url()).pathname.split('/').at(-1)!;
        const nonce = `behavior-response-${++responseNumber}`;
        const partial = route.request().headers()['hx-request'] === 'true';
        return route.fulfill({
            contentType: 'text/html',
            headers: {
                'HX-Nonce': nonce,
                'Content-Security-Policy': `default-src 'self'; script-src 'self' 'nonce-${nonce}'; style-src 'self' 'nonce-${nonce}'`,
            },
            body: documentFor(area, nonce, partial),
        });
    });
});

async function visit(page: Page, area: 'home' | 'test' | 'away') {
    const response = page.waitForResponse(`/behavior-pages/${area}`);
    await page.getByRole('link', { name: { home: 'Home', test: 'Test', away: 'Away' }[area], exact: true }).click();
    expect((await response).request().headers()['hx-request']).toBe('true');
    await expect(page.locator('main')).toHaveAttribute('data-page', area);
}

test('partial field swaps retain sibling validation and submit the current form once', async ({ page }) => {
    const field = (version: number) =>
        `<input aria-label="Name" name="Name" data-val="true" data-val-required="Name ${version} is required">`;
    const form = `<form hx-boost="false" action="/validation-result" target="result">
        <div id="replace-field">${field(0)}</div><span data-valmsg-for="Name"></span>
        <input aria-label="City" name="City" data-val="true" data-val-required="City is required">
        <span data-valmsg-for="City"></span><button>Save</button></form><iframe name="result"></iframe>`;
    await page.route('**/validation-page', (route) =>
        route.fulfill({
            contentType: 'text/html',
            body: documentFor('validation', 'test-nonce', false, form),
        }),
    );
    let version = 0;
    await page.route('**/validation-field', (route) =>
        route.fulfill({ contentType: 'text/html', body: field(++version) }),
    );
    await page.route('**/validation-result?*', (route) => route.fulfill({ contentType: 'text/html', body: 'Saved' }));
    await page.goto('/validation-page');
    const profileForm = page.locator('form');
    await profileForm.evaluate((element) => {
        element.dataset.submissions = '0';
        element.addEventListener('formdata', () => {
            element.dataset.submissions = String(Number(element.dataset.submissions) + 1);
        });
    });
    const save = page.getByRole('button', { name: 'Save' });
    const city = page.getByRole('textbox', { name: 'City' });
    for (const round of [1, 2]) {
        await page.evaluate(() => window.htmx.ajax('get', '/validation-field', { target: '#replace-field' }));
        await city.fill('');
        await save.click();
        await expect(page.locator('[data-valmsg-for="Name"]')).toHaveText(`Name ${round} is required`);
        await expect(page.locator('[data-valmsg-for="City"]')).toHaveText('City is required');
        await expect(profileForm).toHaveAttribute('data-submissions', String(round - 1));

        await page.getByRole('textbox', { name: 'Name' }).fill(`User ${round}`);
        await city.fill('Ankara');
        const saved = page.waitForResponse('**/validation-result?*');
        await save.click();
        expect(new URL((await saved).url()).searchParams.get('Name')).toBe(`User ${round}`);
        await expect(profileForm).toHaveAttribute('data-submissions', String(round));
    }
});

test('native fragment navigation preserves a draft', async ({ page }) => {
    const content = '<a href="#section">Jump</a><section id="section"><input aria-label="Draft"></section>';
    await page.route('**/fragment-page', (route) =>
        route.fulfill({ contentType: 'text/html', body: documentFor('fragment', 'test-nonce', false, content) }),
    );
    await page.goto('/fragment-page');
    const draft = page.getByRole('textbox', { name: 'Draft' });
    await draft.fill('unsaved');
    await page.getByRole('link', { name: 'Jump' }).click();
    await expect(page).toHaveURL(/#section$/);
    await expect(draft).toHaveValue('unsaved');
    await page.goBack();
    await expect(page).toHaveURL(/\/fragment-page$/);
    await expect(draft).toHaveValue('unsaved');
    await page.goForward();
    await expect(page).toHaveURL(/#section$/);
    await expect(draft).toHaveValue('unsaved');
});

test('Back and Forward reload boosted pages', async ({ page }) => {
    await page.goto('/behavior-pages/home');
    await visit(page, 'away');
    await visit(page, 'test');

    const documentResponse = (area: string) =>
        page.waitForResponse(
            (response) =>
                response.url().endsWith(`/behavior-pages/${area}`) && response.request().isNavigationRequest(),
        );
    const back = documentResponse('away');
    await page.goBack();
    expect((await back).ok()).toBe(true);
    await expect(page.locator('main')).toHaveAttribute('data-page', 'away');

    const forward = documentResponse('test');
    await page.goForward();
    expect((await forward).ok()).toBe(true);
    await expect(page.locator('main')).toHaveAttribute('data-page', 'test');
});

for (const [scenario, scriptNonce, crossOrigin, accepted, policy, sendHeader = !policy] of [
    ['matching nonce', 'response-nonce', false, true],
    ['missing nonce', '', false, false],
    ['mismatched nonce', 'wrong-nonce', false, false],
    ['matching nonce from another origin', 'response-nonce', true, false],
    ['CSP nonce fallback', 'response-nonce', false, true, "script-src 'nonce-response-nonce'"],
    [
        'script policy overrides default',
        'response-nonce',
        false,
        false,
        "default-src 'nonce-response-nonce'; script-src 'none'",
    ],
    [
        'header conflicts with second policy',
        'response-nonce',
        false,
        false,
        "script-src 'nonce-response-nonce', script-src-elem 'none'",
        true,
    ],
] as const) {
    test(`safe-nonce filters swapped scripts: ${scenario}`, async ({ page }) => {
        // Omit page CSP so it cannot mask a failure of the extension's script filter.
        await page.route('**/nonce-probe-page', (route) =>
            route.fulfill({ contentType: 'text/html', body: documentFor('away', 'page-nonce', false) }),
        );
        await page.goto('/nonce-probe-page');
        const origin = new URL(page.url()).origin;
        const endpoint = new URL('/nonce-probe-response', crossOrigin ? 'http://127.0.0.1:4176' : origin).href;
        const nonceAttribute = scriptNonce ? ` nonce="${scriptNonce}"` : '';
        await page.route(endpoint, (route) => {
            const headers = {
                'Access-Control-Allow-Origin': origin,
                'Access-Control-Allow-Headers': '*',
                'Access-Control-Expose-Headers': 'HX-Nonce',
                ...(policy && { 'Content-Security-Policy': policy }),
                ...(sendHeader && { 'HX-Nonce': 'response-nonce' }),
            };
            if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers });
            const content = `<p id="swapped">Swapped</p><script id="nonce-probe"${nonceAttribute}>document.body.dataset.probeRan = "yes";</script>`;
            return route.fulfill({
                headers,
                contentType: 'text/html',
                body: content,
            });
        });

        const response = await page.evaluate(
            async ({ endpoint, crossOrigin }) => {
                // Allow this fixture to reach the extension's response-origin check.
                if (crossOrigin) window.htmx.config.selfRequestsOnly = false;
                let response;
                document.addEventListener(
                    'htmx:beforeSwap',
                    (event) => {
                        const { xhr } = event.detail;
                        response = { url: xhr.responseURL, nonce: xhr.getResponseHeader('HX-Nonce') };
                    },
                    { once: true },
                );
                await window.htmx.ajax('get', endpoint, { source: document.body, target: 'main', swap: 'innerHTML' });
                return response;
            },
            { endpoint, crossOrigin },
        );
        expect(response).toEqual({ url: endpoint, nonce: sendHeader ? 'response-nonce' : null });
        await expect(page.locator('main #swapped')).toHaveText('Swapped');
        await expect(page.locator('main script#nonce-probe')).toHaveCount(accepted ? 1 : 0);
        const body = page.locator('body');
        if (accepted) await expect(body).toHaveAttribute('data-probe-ran', 'yes');
        else await expect(body).not.toHaveAttribute('data-probe-ran');
    });
}

test('named form controls cannot bypass nonce filtering', async ({ page }) => {
    await page.goto('/behavior-pages/away');
    await page.route('**/nonce-form', (route) =>
        route.fulfill({
            contentType: 'text/html',
            headers: { 'HX-Nonce': 'response-nonce' },
            body: `<p>Swapped</p><template><form hx-ext="ignore:safe-nonce">
                <input name="getAttribute"><input name="setAttribute">
                </form></template>
                <script>document.body.dataset.unauthorized = 'ran'</script>
                <script nonce="response-nonce">document.body.dataset.authorized = 'ran'</script>`,
        }),
    );
    await page.evaluate(() => window.htmx.ajax('get', '/nonce-form', { source: document.body, target: 'main' }));
    await expect(page.locator('main p')).toHaveText('Swapped');
    await expect(page.locator('body')).toHaveAttribute('data-authorized', 'ran');
    await expect(page.locator('body')).not.toHaveAttribute('data-unauthorized');
    await expect(page.locator('main script')).toHaveCount(1);
});

test('safe-nonce still filters a second swap requested by swapped markup', async ({ page }) => {
    await page.goto('/behavior-pages/away');
    await page.route('**/nonce-controls', (route) =>
        route.fulfill({
            contentType: 'text/html',
            body: `<body><template hx type="partial" hx-target="main">
                <div hx-ext="ignore:other,ignore: safe-nonce" hx-target="#nonce-output">
                    <button data-hx-ext="ignore&#58;safe-nonce" hx-get="/nonce-result">Continue</button>
                    <p>ignore:safe-nonce</p>
                </div><section id="nonce-output"></section>
            </template></body>`,
        }),
    );
    await page.route('**/nonce-result', (route) =>
        route.fulfill({
            contentType: 'text/html',
            headers: { 'HX-Nonce': 'response-nonce' },
            body: `<p>Second swap</p>
                <script>document.body.dataset.unauthorized = 'ran'</script>
                <script nonce="response-nonce">document.body.dataset.authorized = 'ran'</script>`,
        }),
    );
    await page.evaluate(() => window.htmx.ajax('get', '/nonce-controls', { source: document.body, target: 'main' }));
    const controls = page.locator('main div[hx-ext]');
    await controls.getByRole('button', { name: 'Continue' }).click();
    await expect(page.locator('#nonce-output p')).toHaveText('Second swap');
    await expect(page.locator('body')).toHaveAttribute('data-authorized', 'ran');
    await expect(page.locator('body')).not.toHaveAttribute('data-unauthorized');
    await expect(page.locator('main script')).toHaveCount(1);
    await expect(controls).toHaveAttribute('hx-ext', 'ignore:other');
    await expect(controls.locator('p')).toHaveText('ignore:safe-nonce');
});

test('safe-nonce filters scripts before HTMX activates nested partial templates', async ({ page }) => {
    await page.goto('/behavior-pages/away');
    await page.route('**/nonce-partials', (route) =>
        route.fulfill({
            contentType: 'text/html',
            headers: { 'Content-Security-Policy': "script-src 'nonce-response-nonce'" },
            // A leading template parses into the document head. HTMX swaps the response body.
            body: `<body>
                <template hx type="partial" hx-target="main">
                    <template hx type="partial" hx-target="main">
                        <p>Partial content</p>
                        <script>document.body.dataset.unauthorized = 'ran'</script>
                        <script nonce="response-nonce">document.body.dataset.authorized = 'ran'</script>
                    </template>
                </template>
            </body>`,
        }),
    );
    await page.evaluate(() => window.htmx.ajax('get', '/nonce-partials', { source: document.body, target: 'main' }));
    await expect(page.locator('main p')).toHaveText('Partial content');
    await expect(page.locator('main script')).toHaveCount(1);
    await expect(page.locator('body')).toHaveAttribute('data-authorized', 'ran');
    await expect(page.locator('body')).not.toHaveAttribute('data-unauthorized');
});

test('repeated test-page visits execute the bundle but attach only one disposable click handler', async ({ page }) => {
    const errors: string[] = [];
    page.on('pageerror', (error) => errors.push(error.message));
    await page.goto('/behavior-pages/away');
    await page.evaluate(() => {
        window.behaviorRegistrations = 0;
        // oxlint-disable-next-line typescript/unbound-method -- The wrapper forwards its receiver with .apply().
        const register = DRN.Onmount.register;
        DRN.Onmount.register = function (...args) {
            if (args[0] === '[data-js-unhandled-browser-exception]') window.behaviorRegistrations++;
            return register.apply(this, args);
        };
    });

    for (let visitNumber = 1; visitNumber <= 4; visitNumber++) {
        await visit(page, 'test');
        await expect.poll(() => page.evaluate(() => window.behaviorRegistrations)).toBe(visitNumber);
        const snapshot = await page.evaluate(() => {
            const keys = DRN.Onmount.listRegistrationKeys();
            return keys.splice(0);
        });
        expect(snapshot).toContain('[data-js-unhandled-browser-exception]_');
        expect(await page.evaluate(() => DRN.Onmount.listRegistrationKeys())).toEqual(snapshot);
        const button = page.getByRole('button', { name: 'Trigger browser exception' });
        const previousButton = (await button.elementHandle())!;
        await button.click();
        expect(errors).toEqual(Array(visitNumber).fill(intentionalError));

        await visit(page, 'away');
        // A retained detached node must no longer invoke its former handler.
        await previousButton.evaluate((element) => (element as HTMLElement).click());
        expect(errors).toEqual(Array(visitNumber).fill(intentionalError));
        await previousButton.dispose();
    }
});

test('reloading preload preserves app state and services and reports each error once', async ({ page }) => {
    await page.goto('/behavior-pages/away');
    const installed = await page.evaluateHandle(() => {
        DRN.App.Environment = 'Staging';
        return { namespace: DRN, validation: DRN.Validation, mount: DRN.React.mount };
    });
    const preload = page.locator('script[src*="/behavior-assets/app/"]');
    await page.addScriptTag({ url: (await preload.getAttribute('src'))! });
    expect(
        await installed.evaluate((previous) => ({
            namespace: DRN === previous.namespace,
            environment: DRN.App.Environment,
            validation:
                typeof previous.validation?.addProvider === 'function' && DRN.Validation === previous.validation,
            mount: DRN.React.mount === previous.mount,
        })),
    ).toEqual({ namespace: true, environment: 'Staging', validation: true, mount: true });

    const reports = await page.evaluate(() => {
        document.body.dataset.notificationContext = '';
        document.body.dataset.notificationOwner = 'reload-test-user';
        const endpoints: string[] = [];
        navigator.sendBeacon = (url) => {
            endpoints.push(String(url));
            return true;
        };
        window.dispatchEvent(new ErrorEvent('error', { message: 'Preload reload regression' }));
        return endpoints;
    });
    expect(reports).toEqual(['/Api/Sample/ClientError/Report']);
});

test('a home island can mount on the next poll after React becomes available', async ({ page }) => {
    const reactBundle = /\/behavior-assets\/lib\/react\/[^/]+\.js(?:\.br)?$/;
    await page.route(reactBundle, (route) => route.abort(), { times: 1 });
    await page.goto('/behavior-pages/home');
    const host = page.locator('[data-js-hello-react-island]');
    expect(
        await host.evaluate((element) =>
            DRN.React.mount('HelloReact', element as HTMLElement, { title: 'Early mount' }),
        ),
    ).toBeNull();
    const script = page.locator('script[src*="/behavior-assets/lib/react/"]');
    await page.addScriptTag({ url: (await script.getAttribute('src'))! });
    await page.evaluate(() => window.onmount());
    await expect(page.locator('[data-js-hello-react-island] .hello-react')).toBeVisible();
});

test('home islands validate version data before mounting', async ({ page }) => {
    await page.goto('/behavior-pages/home');
    for (const [raw, expected] of [
        ['{', 'React 19'],
        ['{"react":{"major":20}}', 'React 19'],
        ['{"react":"20"}', 'React 20'],
    ] as const) {
        await page.evaluate((versions) => {
            const host = document.createElement('div');
            host.dataset.jsHelloReactIsland = '';
            host.dataset.versions = versions;
            document.body.appendChild(host);
            window.onmount();
        }, raw);
        const island = page.locator('[data-js-hello-react-island]').last();
        await expect(island.getByText(expected, { exact: true })).toBeVisible();
    }
});

test('repeated home visits dispose React roots, intervals, and in-flight title timeouts', async ({ page }) => {
    const clicks: string[] = [];
    const errors: string[] = [];
    page.on('console', (message) => {
        if (message.text().startsWith('Card clicked!')) clicks.push(message.text());
    });
    page.on('pageerror', (error) => errors.push(error.message));
    await page.goto('/behavior-pages/away');
    await page.clock.install();
    await page.clock.pauseAt(Date.now() + 1000);
    await page.evaluate(() => {
        const audit: TimerAudit = (window.homeTimers = {
            intervals: new Set(),
            timeouts: new Set(),
            ticks: 0,
            transitions: 0,
        });
        // Observe actual timer allocation/cancellation; do not replace lifecycle callbacks.
        for (const [method, collection, counter] of [
            ['setInterval', 'intervals', 'ticks'],
            ['setTimeout', 'timeouts', 'transitions'],
        ] as const) {
            const original = window[method].bind(window);
            const trackTimer = (callback: TimerHandler, delay?: number, ...args: unknown[]) => {
                if (typeof callback !== 'function' || !new Error().stack?.includes('/behavior-assets/behaviors/home/'))
                    return original(callback, delay, ...args);
                const id = original(() => {
                    if (collection === 'timeouts') audit[collection].delete(id);
                    audit[counter]++;
                    callback(...args);
                }, delay);
                audit[collection].add(id);
                return id;
            };
            Object.assign(window, { [method]: trackTimer });
        }
        for (const method of ['clearInterval', 'clearTimeout'] as const) {
            const original = window[method].bind(window);
            const clearTimer = (id?: number) => {
                if (id !== undefined) audit.intervals.delete(id);
                if (id !== undefined) audit.timeouts.delete(id);
                return original(id);
            };
            Object.assign(window, { [method]: clearTimer });
        }
    });

    for (let visitNumber = 1; visitNumber <= 3; visitNumber++) {
        await visit(page, 'home');
        const island = page.locator('[data-js-hello-react-island]');
        await expect(island.locator('.hello-react')).toBeVisible();
        await expect.poll(() => page.evaluate(() => window.homeTimers.intervals.size)).toBe(1);
        await island.getByRole('button').first().click();
        expect(clicks).toEqual(Array(visitNumber).fill('Card clicked! Total clicks outside the component: 1'));
        const oldIsland = (await island.elementHandle())!;

        // Leave during the fade, when both the recurring timer and a timeout exist.
        await page.clock.runFor(4000);
        expect(
            await page.evaluate(() => ({
                intervals: homeTimers.intervals.size,
                timeouts: homeTimers.timeouts.size,
                ticks: homeTimers.ticks,
                transitions: homeTimers.transitions,
            })),
        ).toEqual({ intervals: 1, timeouts: 1, ticks: visitNumber, transitions: 0 });
        await visit(page, 'away');
        await expect
            .poll(() => page.evaluate(() => [homeTimers.intervals.size, homeTimers.timeouts.size]))
            .toEqual([0, 0]);
        expect(await oldIsland.evaluate((element) => element.shadowRoot!.querySelector('.hello-react'))).toBeNull();
        await oldIsland.dispose();

        await page.clock.runFor(8000);
        expect(await page.evaluate(() => [homeTimers.ticks, homeTimers.transitions])).toEqual([visitNumber, 0]);
    }
    expect(errors).toEqual([]);
});
