import { test, expect } from '@playwright/test';
import type { Request } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

declare global {
    interface Window {
        notificationMarkupExecuted?: boolean;
        notificationLock: Promise<void>;
        releaseNotificationLock: () => void;
    }
}

test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('html')).toHaveAttribute('data-fixture-ready', 'true');
});

test('a browser action failure shares its copyable reference with the backend report', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await page.route('**/Api/Sample/ClientError/Report', (route) => route.fulfill({ status: 200, body: '' }));
    const reportRequest = page.waitForRequest('**/Api/Sample/ClientError/Report');
    const entry = await page.evaluate(() =>
        window.DRN.ErrorHandler.reportActionError(new Error('Private implementation detail'), {
            operation: 'Save profile',
            message: 'Could not save your profile.',
        }),
    );
    expect(entry).toBeTruthy();
    const report = (await reportRequest).postDataJSON();
    expect(report.clientErrorId).toBe(entry!.clientErrorId);
    await page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true }).click();
    const panel = page.getByRole('dialog', { name: 'Notifications', exact: true });
    await expect(panel.locator('pre')).toHaveCount(0);
    const details = panel.getByRole('button', { name: 'Show details', exact: true });
    await expect(details).toHaveAttribute('aria-expanded', 'false');
    await details.click();
    await expect(panel.getByRole('button', { name: 'Hide details', exact: true })).toHaveAttribute(
        'aria-expanded',
        'true',
    );
    await expect(panel).toContainText(`Client error reference: ${entry!.clientErrorId}`);
    await expect(panel).toContainText('Message: Private implementation detail');
    await expect(panel).toContainText('Stack:');
    await expect(panel.locator('pre strong')).toHaveText([
        'Message:',
        'Source:',
        'Line:',
        'column:',
        'Page:',
        'Stack:',
    ]);
    await expect(page.locator('.toast-body')).toHaveText('Could not save your profile.');
    await panel.getByRole('button', { name: 'Copy reference', exact: true }).click();
    expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(entry!.clientErrorId);
    await panel.getByRole('button', { name: 'Hide details', exact: true }).click();
    await expect(panel.locator('pre')).toBeHidden();
});

test('client error details render markup literally after history reload', async ({ page }) => {
    const detail =
        'Message: <img src=x onerror="window.notificationMarkupExecuted=true">\n' +
        'Source: <b>app.js</b>\nLine: 5, column: 17\nPage: https://sample.test/\n' +
        'Stack:\n    at onClick (<anonymous>:5:23)\nMessage: <script>not a label</script>';
    await page.evaluate(
        (detail) =>
            window.DRN.Notifications.publish({
                type: 'error',
                message: 'Browser failure',
                clientErrorId: 'a'.repeat(32),
                clientErrorDetails: detail,
            }),
        detail,
    );
    await expect
        .poll(() => page.evaluate(() => localStorage.getItem('drn.notifications.v1:/')))
        .toContain('clientErrorDetails');
    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('data-fixture-ready', 'true');
    await page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true }).click();
    const panel = page.getByRole('dialog', { name: 'Notifications', exact: true });
    await panel.getByRole('button', { name: 'Show details', exact: true }).click();
    await expect(panel.locator('pre')).toHaveText(detail);
    await expect(panel.locator('pre strong')).toHaveText([
        'Message:',
        'Source:',
        'Line:',
        'column:',
        'Page:',
        'Stack:',
    ]);
    expect(await panel.locator('pre').textContent()).toBe(detail);
    await expect(panel.locator('pre')).toHaveCSS('white-space', 'pre-wrap');
    await expect(panel.locator('pre img, pre script, pre b')).toHaveCount(0);
    expect(await page.evaluate(() => window.notificationMarkupExecuted)).toBeUndefined();
});

for (const kind of ['error', 'unhandledrejection']) {
    test(`${kind} appears in notification history with a shared client reference`, async ({ page }) => {
        await page.route('**/Api/Sample/ClientError/Report', (route) => route.fulfill({ status: 200, body: '' }));
        const reportRequest = page.waitForRequest('**/Api/Sample/ClientError/Report');
        await page.evaluate((kind) => {
            if (kind === 'error') {
                setTimeout(() => {
                    throw new Error('Private global diagnostic');
                }, 0);
            } else {
                void Promise.reject(new Error('Private global diagnostic'));
            }
        }, kind);
        const report = (await reportRequest).postDataJSON();
        expect(report.clientErrorId).toMatch(/^[a-f0-9]{32}$/);
        expect(report.message).toContain('Private global diagnostic');
        await page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true }).click();
        const panel = page.getByRole('dialog', { name: 'Notifications', exact: true });
        await expect(panel).toContainText('An unexpected error occurred');
        await panel.getByRole('button', { name: 'Show details', exact: true }).click();
        await expect(panel).toContainText(`Client error reference: ${report.clientErrorId}`);
        await expect(panel.locator('pre')).toContainText(`Message: ${report.message}`);
        await expect(panel).toContainText('Stack:');
        await expect(page.locator('.toast')).toHaveCount(1);
        await expect(page.locator('.toast-body')).toHaveText(
            'A page encountered a problem. If you were completing an action, check its result before trying again.',
        );
        expect(await page.evaluate(() => window.DRN.Notifications.list())).toHaveLength(1);
    });
}

test('uncaught error floods keep three visible toasts and five unique history entries', async ({ page }) => {
    const reports = [];
    await page.route('**/Api/Sample/ClientError/Report', (route) => {
        reports.push(route.request().postDataJSON());
        return route.fulfill({ status: 200, body: '' });
    });
    await page.evaluate(() => {
        for (let index = 0; index < 20; index++) {
            const event = new ErrorEvent('error', { message: `Private failure ${index}`, filename: 'test.js' });
            window.dispatchEvent(event);
            window.dispatchEvent(new ErrorEvent('error', { message: `Private failure ${index}`, filename: 'test.js' }));
        }
    });
    await expect.poll(() => reports.length).toBe(5);
    await expect(page.locator('.toast')).toHaveCount(3);
    expect(await page.evaluate(() => window.DRN.Notifications.list())).toHaveLength(5);
    await expect(page.locator('.toast-container')).not.toContainText('Private failure');
});

test('toast bursts retain history without queuing excess presentation and recover after cooldown', async ({ page }) => {
    await page.clock.install();
    await page.evaluate(() => {
        for (let index = 0; index < 20; index++)
            window.DRN.Toast.show({ type: 'error', message: `Failure ${index}`, duration: 30000 });
    });
    await expect(page.locator('.toast-body')).toHaveText(['Failure 2', 'Failure 3', 'Failure 4']);
    expect(await page.evaluate(() => window.DRN.Notifications.list())).toHaveLength(20);
    await page.clock.fastForward(10001);
    await expect(page.locator('.toast-body')).toHaveText(['Failure 2', 'Failure 3', 'Failure 4']);
    await page.evaluate(() => window.DRN.Toast.error('Later failure'));
    await expect(page.locator('.toast-body')).toHaveText(['Failure 3', 'Failure 4', 'Later failure']);
});

test('closed notification panels defer list rendering while badges stay current', async ({ page }) => {
    const list = page.locator('#drn-notification-list');
    await page.evaluate(() => {
        window.DRN.Notifications.publish({ message: 'First feedback' });
        window.DRN.Notifications.publish({ message: 'Second feedback' });
    });
    await expect(page.getByRole('button', { name: 'Notifications, 2 unseen', exact: true })).toBeVisible();
    await expect(list).toBeEmpty();
    await page.getByRole('button', { name: 'Notifications, 2 unseen', exact: true }).click();
    const panel = page.getByRole('dialog', { name: 'Notifications', exact: true });
    await expect(list.locator('[data-notification-group]')).toHaveCount(2);
    await expect(panel).toHaveClass(/\bshow\b/);
    await expect(page.locator('[data-notification-count]')).toBeHidden();
    await panel.getByRole('button', { name: 'Close notifications' }).click();
    await expect(panel).toBeHidden();

    await page.evaluate(() => window.DRN.Notifications.publish({ message: 'Third feedback' }));
    await expect(page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true })).toBeVisible();
    await expect(list.locator('[data-notification-group]')).toHaveCount(2);
    await page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true }).click();
    await expect(list.locator('[data-notification-group]')).toHaveCount(3);
    await expect(list).toContainText('Third feedback');
});

for (const hosted of [true, false]) {
    test(`notification panel stays above overlapping toasts, content host: ${hosted}`, async ({ page }) => {
        await page.setViewportSize({ width: 480, height: 800 });
        await page.evaluate((hosted) => {
            if (hosted) {
                const main = document.getElementById('drn-main-content')!;
                main.setAttribute('data-toast-host', '');
                main.style.position = 'relative';
            }
            window.DRN.Toast.show({ type: 'error', message: 'Please try again.', duration: 30000 });
            bootstrap.Offcanvas.getOrCreateInstance(
                document.getElementById('offCanvasSidebarActionCanvasNotifications')!,
            ).show();
        }, hosted);
        const panel = page.getByRole('dialog', { name: 'Notifications', exact: true });
        await expect(panel).toBeVisible();
        await expect(panel).toHaveClass(/\bshow\b/);
        await expect
            .poll(() =>
                page.evaluate(() => {
                    const panel = document.getElementById('offCanvasSidebarActionCanvasNotifications');
                    const toast = document.querySelector('.toast');
                    if (!toast || !panel) return false;
                    const panelRect = panel.getBoundingClientRect();
                    const toastRect = toast.getBoundingClientRect();
                    const left = Math.max(panelRect.left, toastRect.left);
                    const right = Math.min(panelRect.right, toastRect.right);
                    const top = Math.max(panelRect.top, toastRect.top);
                    const bottom = Math.min(panelRect.bottom, toastRect.bottom);
                    return (
                        right > left &&
                        bottom > top &&
                        panel.contains(document.elementFromPoint((left + right) / 2, (top + bottom) / 2))
                    );
                }),
            )
            .toBe(true);
        await panel.getByRole('searchbox', { name: 'Search notifications' }).fill('Please try again');
        await expect(panel.locator('#drn-notification-list')).toContainText('Please try again.');
        await panel.getByRole('button', { name: 'Close notifications' }).click();
        await expect(panel).toBeHidden();
        await expect(page.locator('.toast-body')).toHaveText('Please try again.');
        expect(await page.evaluate(() => window.DRN.Notifications.list().length)).toBe(1);
    });
}

test('competing tabs retain publications and dismissals queued behind a real Web Lock', async ({ page, context }) => {
    const other = await context.newPage();
    await other.goto('/');
    await expect(other.locator('html')).toHaveAttribute('data-fixture-ready', 'true');
    const old = await page.evaluate(() => window.DRN.Notifications.publish({ message: 'Dismiss this entry' }));
    await expect.poll(() => other.evaluate(() => window.DRN.Notifications.list().length)).toBe(1);

    // The lock is held before either tab mutates, forcing both to use the same saved snapshot.
    await page.evaluate(
        () =>
            new Promise<void>((resolve) => {
                window.notificationLock = navigator.locks.request(
                    'drn.notifications.v1:/',
                    () =>
                        new Promise<void>((release) => {
                            window.releaseNotificationLock = release;
                            resolve();
                        }),
                );
            }),
    );
    try {
        await page.evaluate((id) => window.DRN.Notifications.dismiss([id]), old!.id);
        await Promise.all([
            page.evaluate(() => window.DRN.Notifications.publish({ message: 'First tab addition' })),
            other.evaluate(() => window.DRN.Notifications.publish({ message: 'Second tab addition' })),
        ]);
    } finally {
        await page.evaluate(async () => {
            window.releaseNotificationLock();
            await window.notificationLock;
        });
    }
    const expected = ['First tab addition', 'Second tab addition'];
    for (const tab of [page, other]) {
        await expect
            .poll(() =>
                tab.evaluate(() =>
                    window.DRN.Notifications.list()
                        .map((entry) => entry.message)
                        .sort(),
                ),
            )
            .toEqual(expected);
    }
    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('data-fixture-ready', 'true');
    expect(
        await page.evaluate(() =>
            window.DRN.Notifications.list()
                .map((entry) => entry.message)
                .sort(),
        ),
    ).toEqual(expected);
    await other.close();
});

// Each header can act before beforeSwap. These exercise our account boundary with actual HTMX responses.
for (const [header, value] of [
    ['HX-Redirect', '/unexpected-navigation'],
    ['HX-Refresh', 'true'],
    ['HX-Location', '/unexpected-navigation'],
    ['HX-Trigger', JSON.stringify({ showToast: { type: 'success', message: 'Old account feedback' } })],
] as const) {
    test(`an old account response cannot act through ${header}`, async ({ page }) => {
        await page.evaluate(() => window.DRN.Notifications.publish({ message: 'Account A feedback' }));
        await expect(page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true })).toBeVisible();
        const pending = Promise.withResolvers<void>();
        await page.route('**/operations/pending', async (route) => {
            await pending.promise;
            await route.fulfill({
                status: 200,
                contentType: 'text/html',
                headers: { [header]: value },
                body: '<p>Old account result</p>',
            });
        });
        await page.route('**/operations/account', (route) =>
            route.fulfill({
                contentType: 'text/html',
                body: '<div data-notification-context data-notification-app="/" data-notification-owner="account-b"></div>',
            }),
        );
        const unexpectedRequests: string[] = [];
        page.on('request', (request) => {
            if (request.isNavigationRequest() || new URL(request.url()).pathname === '/unexpected-navigation')
                unexpectedRequests.push(request.url());
        });
        await page.route('**/unexpected-navigation', (route) =>
            route.fulfill({
                contentType: 'text/html',
                body: '<p>Unexpected navigation</p>',
            }),
        );

        try {
            const started = page.waitForRequest('**/operations/pending');
            await page.getByRole('button', { name: 'Start delayed operation' }).click();
            await started;
            await page.getByRole('button', { name: 'Change account' }).click();
            await expect(page.locator('[data-notification-context]')).toHaveAttribute(
                'data-notification-owner',
                'account-b',
            );
            // Wait for the application to clear the old account's history before releasing its response.
            await expect(page.getByRole('button', { name: 'Notifications', exact: true })).toBeVisible();
            await expect(page.locator('[data-notification-count]')).toBeHidden();
            pending.resolve();
            await expect(page.locator('html')).toHaveAttribute('data-completed-requests', '2');

            await expect(page.locator('html')).toHaveAttribute('data-pending-response-rejected', 'true');
            await expect(page).toHaveURL('/');
            await expect(page.getByLabel('Unsubmitted draft')).toHaveValue('Draft to preserve');
            await expect(page.locator('#operation-result')).toHaveText('Original result');
            await expect(page.getByRole('button', { name: 'Notifications', exact: true })).toBeVisible();
            await expect(page.locator('[data-notification-count]')).toBeHidden();
            await expect(page.locator('.toast')).toHaveCount(0);
            expect(unexpectedRequests).toEqual([]);
        } finally {
            pending.resolve();
        }
    });
}

test('current account response feedback remains visible and is retained once', async ({ page }) => {
    await page.route('**/operations/pending', (route) =>
        route.fulfill({
            contentType: 'text/html',
            body: '<p>Operation completed</p>',
            headers: { 'HX-Trigger': JSON.stringify({ showToast: { type: 'success', message: 'Profile updated' } }) },
        }),
    );
    await page.getByRole('button', { name: 'Start delayed operation' }).click();

    await expect(page.locator('#operation-result')).toHaveText('Operation completed');
    await expect(page.locator('.toast-body')).toHaveText('Profile updated');
    await expect(page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true })).toBeVisible();
});

test('toasts retain notification metadata and deduplicate history by source identity', async ({ page }) => {
    const input = {
        type: 'error' as const,
        title: 'Save failed',
        message: 'Choose another name.',
        sourceId: 'save-profile:one',
        problemType: 'urn:sample:problem:duplicate-name',
        status: 409,
        requestId: 'profile-1',
        operation: 'Save profile',
        target: 'profile',
        persist: false,
    };
    const entries = await page.evaluate((input) => {
        window.DRN.Toast.show({ ...input, duration: 30_000 });
        window.DRN.Toast.show({ ...input, duration: 30_000 });
        return window.DRN.Notifications.list();
    }, input);

    expect(entries).toHaveLength(1);
    expect(entries[0]).toMatchObject(input);
    expect(entries[0]).not.toHaveProperty('duration');
    await expect(page.locator('.toast-body')).toHaveText([input.message, input.message]);
    await expect(page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true })).toBeVisible();
});

for (const removal of ['timeout', 'close button', 'HTMX cleanup', 'overflow']) {
    test(`toast ${removal} preserves history and its persisted copy`, async ({ page }) => {
        const entries = await page.evaluate((removal) => {
            const messages = removal === 'overflow' ? ['First', 'Second', 'Third', 'Fourth'] : ['Export completed'];
            for (const message of messages)
                window.DRN.Toast.show({ message, duration: removal === 'timeout' ? 1000 : 30_000 });
            return window.DRN.Notifications.list();
        }, removal);
        expect(entries).toHaveLength(removal === 'overflow' ? 4 : 1);
        if (removal === 'overflow') {
            expect(entries.map((entry) => entry.message).sort()).toEqual(['First', 'Fourth', 'Second', 'Third']);
            await expect(page.locator('.toast-body')).toHaveText(['Second', 'Third', 'Fourth']);
        } else {
            await expect(page.locator('.toast')).toBeVisible();
        }

        if (removal === 'close button') {
            await page.locator('.toast').getByRole('button', { name: 'Close', exact: true }).click();
        } else if (removal === 'HTMX cleanup') {
            await page.evaluate(() =>
                document.dispatchEvent(
                    new CustomEvent('htmx:beforeCleanupElement', {
                        detail: { elt: document.querySelector('.toast-container') },
                    }),
                ),
            );
        }

        await expect(page.locator('.toast')).toHaveCount(removal === 'overflow' ? 3 : 0);
        expect(await page.evaluate(() => window.DRN.Notifications.list())).toEqual(entries);
        const triggerName = `Notifications, ${entries.length} unseen`;
        await expect(page.getByRole('button', { name: triggerName, exact: true })).toBeVisible();

        await page.reload();
        await expect(page.getByRole('button', { name: triggerName, exact: true })).toBeVisible();
        expect(await page.evaluate(() => window.DRN.Notifications.list())).toEqual(entries);
        await expect(page.locator('.toast')).toHaveCount(0);
    });
}

test('toast-only feedback and history-only notifications remain independent', async ({ page }) => {
    const entry = await page.evaluate(() => window.DRN.Notifications.publish({ message: 'Report ready' }));
    await expect(page.locator('.toast')).toHaveCount(0);
    await page.evaluate(() => window.DRN.Toast.show({ message: 'Copied', record: false, duration: 30_000 }));
    await expect(page.locator('.toast-body')).toHaveText('Copied');
    expect(await page.evaluate(() => window.DRN.Notifications.list())).toEqual([entry]);
    await page.locator('.toast').getByRole('button', { name: 'Close', exact: true }).click();
    await expect(page.locator('.toast')).toHaveCount(0);
    expect(await page.evaluate(() => window.DRN.Notifications.list())).toEqual([entry]);
    await page.reload();
    await expect(page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true })).toBeVisible();
    expect(await page.evaluate(() => window.DRN.Notifications.list())).toEqual([entry]);
});

test('explicit history dismissal neither closes the toast nor gets undone by closing it', async ({ page }) => {
    await page.evaluate(() => {
        window.DRN.Toast.show({ message: 'Export completed', duration: 30_000 });
        const entry = window.DRN.Notifications.list()[0]!;
        window.DRN.Notifications.dismiss([entry.id]);
    });
    await expect(page.locator('.toast-body')).toHaveText('Export completed');
    expect(await page.evaluate(() => window.DRN.Notifications.list())).toEqual([]);
    await page.locator('.toast').getByRole('button', { name: 'Close', exact: true }).click();
    await expect(page.locator('.toast')).toHaveCount(0);
    expect(await page.evaluate(() => window.DRN.Notifications.list())).toEqual([]);
    await expect(page.getByRole('button', { name: 'Notifications', exact: true })).toBeVisible();
    await expect(page.locator('[data-notification-count]')).toBeHidden();
});

test('prefixed field errors preserve input, render public text safely and clear only their own validation', async ({
    page,
}) => {
    const message = '<img src=x> is not a valid name.';
    await page.route('**/operations/profile', (route) =>
        route.fulfill({
            status: 422,
            contentType: 'application/problem+json',
            body: JSON.stringify({
                title: 'Profile could not be saved',
                errors: { 'Input.Name': [message] },
                requestId: 'profile-1',
            }),
        }),
    );
    await page.route('**/operations/preferences', (route) =>
        route.fulfill({
            status: 409,
            contentType: 'application/problem+json',
            body: JSON.stringify({
                title: 'Review your preferences',
                errors: { '': ['Preferences changed elsewhere'] },
            }),
        }),
    );
    const profile = page.getByRole('form', { name: 'Profile', exact: true });
    const name = page.getByRole('textbox', { name: 'Display name', exact: true });
    const summary = profile.getByRole('region', { name: 'Request error' });
    await name.fill('My unsaved name');
    await page.getByRole('button', { name: 'Save profile', exact: true }).click();

    await expect(summary).toBeFocused();
    await expect(name).toHaveValue('My unsaved name');
    await expect(name).toHaveAttribute('aria-invalid', 'true');
    await expect(name).toHaveAccessibleDescription(`Choose a public display name. Existing validation hint ${message}`);
    const scan = await new AxeBuilder({ page }).include('#profile').analyze();
    expect(scan.violations).toEqual([]);
    await expect(profile.locator('img')).toHaveCount(0);
    const errorLink = summary.getByRole('button', { name: `Input.Name: ${message}`, exact: true });
    await errorLink.focus();
    await errorLink.press('Enter');
    await expect(name).toBeFocused();
    await expect(page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true })).toBeVisible();

    await page.getByRole('button', { name: 'Save preferences', exact: true }).click();
    const otherSummary = page
        .getByRole('form', { name: 'Preferences', exact: true })
        .getByRole('region', { name: 'Request error' });
    await expect(otherSummary).toContainText('Preferences changed elsewhere');

    await page.unroute('**/operations/profile');
    await page.route('**/operations/profile', (route) =>
        route.fulfill({
            contentType: 'text/html',
            body: '<p>Profile saved</p>',
        }),
    );
    await page.getByRole('button', { name: 'Save profile', exact: true }).click();
    await expect(page.locator('#profile-result')).toHaveText('Profile saved');
    await expect(summary).toHaveCount(0);
    await expect(otherSummary).toContainText('Preferences changed elsewhere');
    await expect(name).toHaveValue('My unsaved name');
    await expect(name).toHaveAttribute('aria-invalid', 'false');
    await expect(name).toHaveAttribute('aria-describedby', 'name-help native-validation');
    await expect(page.locator('#native-validation')).toHaveText('Existing validation hint');
    await expect(page.getByRole('button', { name: 'Notifications, 2 unseen', exact: true })).toBeVisible();
});

for (const permitted of [true, false]) {
    test(`HTML validation ${permitted ? 'swaps with explicit permission' : 'does not swap without permission'}`, async ({
        page,
    }) => {
        const message = 'Correct the profile name.';
        await page.route('**/operations/profile', (route) =>
            route.fulfill({
                status: 422,
                contentType: 'text/html',
                headers: permitted ? { 'X-DRN-Validation': 'true' } : {},
                body: `<p class="text-danger">${message}</p>`,
            }),
        );
        const profile = page.getByRole('form', { name: 'Profile', exact: true });
        const name = page.getByRole('textbox', { name: 'Display name', exact: true });
        const summary = profile.getByRole('region', { name: 'Request error' });
        await name.fill('My unsaved name');
        await page.getByRole('button', { name: 'Save profile', exact: true }).click();
        await expect(page.locator('html')).toHaveAttribute('data-completed-requests', '1');

        await expect(name).toHaveValue('My unsaved name');
        await expect(name).toHaveAttribute('aria-describedby', 'name-help native-validation');
        await expect(page.locator('#native-validation')).toHaveText('Existing validation hint');
        if (permitted) {
            await expect(page.locator('.toast')).toHaveCount(0);
            await expect(page.locator('#profile-result')).toHaveText(message);
            await expect(summary).toHaveCount(0);
            await expect(page.getByRole('button', { name: 'Notifications', exact: true })).toBeVisible();
            await expect(page.locator('[data-notification-count]')).toBeHidden();
        } else {
            await expect(page.locator('#profile-result')).toBeEmpty();
            await expect(summary).toHaveCount(0);
            await expect(page.locator('.toast-body')).toHaveText('The request could not be completed.');
            await expect(page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true })).toBeVisible();
        }
    });
}

test('opening an action panel clears only its highlight and preserves its count', async ({ page }) => {
    await page.evaluate(() => window.DRN.Notifications.publish({ message: 'Report ready' }));
    const notifications = page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true });
    const messages = page.getByRole('button', { name: 'Messages', exact: true });
    await expect(messages).toHaveClass(/text-bg-warning/);
    await messages.click();
    const panel = page.getByRole('dialog', { name: 'Messages', exact: true });
    await expect(panel).toBeVisible();
    await expect(messages).not.toHaveClass(/text-bg-warning/);
    await expect(notifications).toHaveClass(/text-bg-warning/);
    await expect(page.locator('[data-message-count]')).toHaveText('9+');
    await expect(page.locator('[data-message-count]')).toHaveClass(/text-bg-danger/);
    await panel.getByRole('button', { name: 'Close messages' }).click();
    await expect(panel).toBeHidden();
    await expect(messages).not.toHaveClass(/text-bg-warning/);
    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('data-fixture-ready', 'true');
    await expect(messages).not.toHaveClass(/text-bg-warning/);
    await expect(page.locator('[data-message-count]')).toHaveText('9+');

    // A new activity version must re-highlight even when a capped count stays at 9+.
    await messages.evaluate((control) => {
        control.dataset.actionbarAttentionVersion = 'new-message';
        document.dispatchEvent(new Event('htmx:load'));
    });
    await expect(messages).toHaveClass(/text-bg-warning/);
    await messages.click();
    await expect(panel).toBeVisible();
    await expect(messages).not.toHaveClass(/text-bg-warning/);
    await panel.getByRole('button', { name: 'Close messages' }).click();
    await expect(panel).toBeHidden();
    await page.locator('[data-notification-context]').evaluate((marker) => {
        marker.dataset.notificationOwner = 'account-b';
        document.dispatchEvent(new Event('htmx:load'));
    });
    await expect(messages).toHaveClass(/text-bg-warning/);
});

test('overlapping acknowledgments of different panels preserve both updates', async ({ page, context }) => {
    const other = await context.newPage();
    await other.goto('/');
    await expect(other.locator('html')).toHaveAttribute('data-fixture-ready', 'true');

    // Both handlers read the same snapshot, reproducing the lost-update interleaving deterministically.
    for (const tab of [page, other]) {
        await tab.evaluate(() => {
            // oxlint-disable-next-line typescript/unbound-method -- The wrappers forward their receiver with .call().
            const { getItem, setItem } = Storage.prototype;
            const snapshot = new Map(Object.keys(localStorage).map((key) => [key, localStorage.getItem(key)]));
            Storage.prototype.getItem = function (key) {
                return this === localStorage && key.startsWith('drn.actionbar.')
                    ? (snapshot.get(key) ?? null)
                    : getItem.call(this, key);
            };
            Storage.prototype.setItem = function (key, value) {
                if (this === localStorage && key.startsWith('drn.actionbar.')) {
                    Storage.prototype.getItem = getItem;
                    Storage.prototype.setItem = setItem;
                }
                return setItem.call(this, key, value);
            };
        });
    }
    await page.getByRole('button', { name: 'Messages', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Messages', exact: true })).not.toHaveClass(/text-bg-warning/);
    await other.getByRole('button', { name: 'Support', exact: true }).click();
    await expect(other.getByRole('button', { name: 'Support', exact: true })).not.toHaveClass(/text-bg-warning/);

    for (const tab of [page, other]) {
        await tab.evaluate(() => document.dispatchEvent(new Event('htmx:load')));
        for (const name of ['Messages', 'Support'])
            await expect(tab.getByRole('button', { name, exact: true })).not.toHaveClass(/text-bg-warning/);
        await tab.reload();
        await expect(tab.locator('html')).toHaveAttribute('data-fixture-ready', 'true');
        for (const name of ['Messages', 'Support'])
            await expect(tab.getByRole('button', { name, exact: true })).not.toHaveClass(/text-bg-warning/);
    }
    await other.close();
});

test('legacy attendance survives migration and failed writes remain acknowledged locally', async ({ page }) => {
    await page.evaluate(() =>
        localStorage.setItem(
            'drn.actionbar.v2:["/","account-a"]',
            JSON.stringify({ owner: 'account-a', entries: [['#messages-panel', '9+']] }),
        ),
    );
    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('data-fixture-ready', 'true');
    await expect(page.getByRole('button', { name: 'Messages', exact: true })).not.toHaveClass(/text-bg-warning/);
    await page.evaluate(() => {
        // oxlint-disable-next-line typescript/unbound-method -- The wrapper forwards its receiver with .call().
        const setItem = Storage.prototype.setItem;
        Storage.prototype.setItem = function (key, value) {
            if (this === localStorage && key.startsWith('drn.actionbar.'))
                throw new DOMException('Quota exceeded', 'QuotaExceededError');
            return setItem.call(this, key, value);
        };
    });
    await page.getByRole('button', { name: 'Support', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Support', exact: true })).not.toHaveClass(/text-bg-warning/);
    await page.evaluate(() => document.dispatchEvent(new Event('htmx:load')));
    await expect(page.getByRole('button', { name: 'Support', exact: true })).not.toHaveClass(/text-bg-warning/);
    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('data-fixture-ready', 'true');
    await expect(page.getByRole('button', { name: 'Support', exact: true })).toHaveClass(/text-bg-warning/);
    await expect(page.getByRole('button', { name: 'Messages', exact: true })).not.toHaveClass(/text-bg-warning/);
});

test('new and existing tabs share actionbar attendance and unseen notifications', async ({ page, context }) => {
    await page.getByRole('button', { name: 'Messages', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Messages', exact: true })).not.toHaveClass(/text-bg-warning/);
    await page.getByRole('button', { name: 'Close messages' }).click();
    await page.evaluate(() => window.DRN.Notifications.publish({ message: 'Shared report ready' }));

    const other = await context.newPage();
    await other.goto('/');
    await expect(other.locator('html')).toHaveAttribute('data-fixture-ready', 'true');
    await expect(other.getByRole('button', { name: 'Messages', exact: true })).not.toHaveClass(/text-bg-warning/);
    await expect(other.locator('[data-message-count]')).toHaveText('9+');
    await other.getByRole('button', { name: 'Notifications, 1 unseen', exact: true }).click();
    await expect(page.locator('[data-notification-count]')).toBeHidden();
    await expect(page.getByRole('button', { name: 'Notifications', exact: true })).not.toHaveClass(/text-bg-warning/);
    await expect(other.locator('[data-notification-count]')).toBeHidden();

    await other.evaluate(() => window.DRN.Notifications.publish({ message: 'New shared activity' }));
    await expect(page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true })).toHaveClass(
        /text-bg-warning/,
    );
    await expect(page.locator('.toast')).toHaveCount(0);
    await other.evaluate(() => window.DRN.Notifications.clear());
    await expect(page.locator('[data-notification-count]')).toBeHidden();

    // An already-open tab also receives attendance updates for the same activity revision.
    for (const tab of [page, other]) {
        await tab.getByRole('button', { name: 'Messages', exact: true }).evaluate((control) => {
            control.dataset.actionbarAttentionVersion = 'next-revision';
            document.dispatchEvent(new Event('htmx:load'));
        });
    }
    await expect(other.getByRole('button', { name: 'Messages', exact: true })).toHaveClass(/text-bg-warning/);
    await page.getByRole('button', { name: 'Messages', exact: true }).click();
    await expect(other.getByRole('button', { name: 'Messages', exact: true })).not.toHaveClass(/text-bg-warning/);
    await other.close();
});

test('the bell stays visible and counts only unseen failures without a page banner', async ({ page }) => {
    const clientReports: Request[] = [];
    await page.route('**/Api/Sample/ClientError/Report', (route) => {
        clientReports.push(route.request());
        return route.fulfill({ status: 200, body: '' });
    });
    const badge = page.locator('[data-notification-count]');
    await expect(page.getByRole('button', { name: 'Notifications', exact: true })).toBeVisible();
    await expect(badge).toBeHidden();
    await page.route('**/operations/pending', (route) =>
        route.fulfill({
            status: 400,
            contentType: 'application/problem+json',
            body: JSON.stringify({ title: 'Bad Request', requestId: 'request-one' }),
        }),
    );
    const operation = page.getByRole('button', { name: 'Start delayed operation' });
    await operation.click();
    await expect(page.locator('.toast-body')).toHaveText(
        'Please try again. If the problem continues, contact support and share the error details from Notifications.',
    );
    await expect(page.locator('[data-drn-problem-summary]')).toHaveCount(0);
    await expect(page.locator('#operation-result')).toHaveText('Original result');
    await expect(operation).toBeFocused();
    await expect(badge).toHaveText('1');
    await expect(page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true })).toHaveClass(
        /text-bg-warning/,
    );
    await expect(badge).toHaveClass(/text-bg-danger/);
    const entries = await page.evaluate(() => window.DRN.Notifications.list());
    expect(entries).toHaveLength(1);
    expect(entries[0]).toMatchObject({ seen: false, status: 400, requestId: 'request-one' });
    expect(clientReports).toHaveLength(0);

    await page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true }).click();
    const panel = page.getByRole('dialog', { name: 'Notifications', exact: true });
    await expect(panel).toBeVisible();
    await expect(badge).toBeHidden();
    await expect(panel).toContainText(
        'Please try again. If the problem continues, contact support and share the error details from Notifications.',
    );
    await expect(page.getByRole('button', { name: 'Notifications', exact: true })).not.toHaveClass(/text-bg-warning/);
    await panel.getByRole('button', { name: 'Show details', exact: true }).click();
    await expect(panel).toContainText('Share this reference with support so they can investigate the error.');
    expect(await page.evaluate(() => window.DRN.Notifications.list())).toEqual([{ ...entries[0], seen: true }]);
    await panel.getByRole('button', { name: 'Close notifications' }).click();
    await expect(panel).toBeHidden();
    await expect(page.getByRole('button', { name: 'Notifications', exact: true })).toBeFocused();

    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('data-fixture-ready', 'true');
    await expect(page.getByRole('button', { name: 'Notifications', exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Notifications', exact: true })).not.toHaveClass(/text-bg-warning/);
    await expect(badge).toBeHidden();
    await operation.click();
    await expect(page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true })).toBeVisible();
    expect(await page.evaluate(() => window.DRN.Notifications.list())).toHaveLength(2);
});

test('keyboard pagination keeps focus on the first new occurrence, including the final page', async ({ page }) => {
    await page.evaluate(() => {
        const now = Date.now();
        window.DRN.Notifications.restore(
            Array.from({ length: 101 }, (_, index) => ({
                id: `entry-${index}`,
                type: 'error',
                title: 'Save failed',
                message: '',
                seen: false,
                persist: true,
                clientErrorId: '',
                clientErrorDetails: '',
                sourceId: '',
                problemType: '',
                status: 0,
                operation: '',
                target: '',
                createdAt: now - index,
                requestId: `reference-${index}`,
            })),
        );
    });
    await page.getByRole('button', { name: 'Notifications, 101 unseen', exact: true }).click();
    const panel = page.getByRole('dialog', { name: 'Notifications', exact: true });
    // Bootstrap activates its focus trap when the opening transition completes.
    await expect(panel).toHaveClass(/\bshow\b/);
    await panel.getByRole('searchbox', { name: 'Search notifications' }).click();
    const summary = panel.getByRole('button', { name: 'Show details', exact: true });
    await summary.focus();
    await summary.press('Enter');
    const rows = panel.locator('[data-notification-details][data-expanded="true"]').getByRole('listitem');
    await expect(rows).toHaveCount(50);
    await expect(panel.getByRole('button', { name: 'Hide details', exact: true })).toBeFocused();
    const scan = await new AxeBuilder({ page }).include('#offCanvasSidebarActionCanvasNotifications').analyze();
    expect(scan.violations).toEqual([]);

    const more = panel.getByRole('button', { name: 'Load more occurrences', exact: true });
    await more.focus();
    await more.press('Enter');
    await expect(rows).toHaveCount(100);
    await expect(rows.nth(50)).toBeFocused();
    await expect(rows.nth(50)).toContainText('Request reference: reference-50');
    await expect(more).toBeVisible();

    await more.focus();
    await more.press('Enter');
    await expect(rows).toHaveCount(101);
    await expect(rows.nth(100)).toBeFocused();
    await expect(rows.nth(100)).toContainText('Request reference: reference-100');
    await expect(more).toHaveCount(0);
});
