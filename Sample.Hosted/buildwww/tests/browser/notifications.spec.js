import { test, expect } from '@playwright/test';

test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('html')).toHaveAttribute('data-fixture-ready', 'true');
});

test('competing tabs retain publications and dismissals queued behind a real Web Lock', async ({ page, context }) => {
    const other = await context.newPage();
    await other.goto('/');
    await expect(other.locator('html')).toHaveAttribute('data-fixture-ready', 'true');
    const old = await page.evaluate(() => window.DRN.Notifications.publish({ message: 'Dismiss this entry' }));
    await expect.poll(() => other.evaluate(() => window.DRN.Notifications.list().length)).toBe(1);

    // The lock is held before either tab mutates, forcing both to use the same saved snapshot.
    await page.evaluate(() => new Promise(resolve => {
        window.notificationLock = navigator.locks.request('drn.notifications.v1:/', () => new Promise(release => {
            window.releaseNotificationLock = release;
            resolve();
        }));
    }));
    try {
        await page.evaluate(id => window.DRN.Notifications.dismiss([id]), old.id);
        await Promise.all([
            page.evaluate(() => window.DRN.Notifications.publish({ message: 'First tab addition' })),
            other.evaluate(() => window.DRN.Notifications.publish({ message: 'Second tab addition' }))
        ]);
    } finally {
        await page.evaluate(async () => {
            window.releaseNotificationLock();
            await window.notificationLock;
        });
    }
    const expected = ['First tab addition', 'Second tab addition'];
    for (const tab of [page, other]) {
        await expect.poll(() => tab.evaluate(() => window.DRN.Notifications.list().map(entry => entry.message).sort()))
            .toEqual(expected);
    }
    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('data-fixture-ready', 'true');
    expect(await page.evaluate(() => window.DRN.Notifications.list().map(entry => entry.message).sort())).toEqual(expected);
    await other.close();
});

// Each header can act before beforeSwap. These exercise our account boundary with actual HTMX responses.
for (const [header, value] of [
    ['HX-Redirect', '/unexpected-navigation'],
    ['HX-Refresh', 'true'],
    ['HX-Location', '/unexpected-navigation'],
    ['HX-Trigger', JSON.stringify({ showToast: { type: 'success', message: 'Old account feedback' } })]
]) {
    test(`an old account response cannot act through ${header}`, async ({ page }) => {
        await page.evaluate(() => window.DRN.Notifications.publish({ message: 'Account A feedback' }));
        await expect(page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true })).toBeVisible();
        const pending = Promise.withResolvers();
        await page.route('**/operations/pending', async route => {
            await pending.promise;
            await route.fulfill({ status: 200, contentType: 'text/html',
                headers: { [header]: value }, body: '<p>Old account result</p>' });
        });
        await page.route('**/operations/account', route => route.fulfill({
            contentType: 'text/html',
            body: '<div data-notification-context data-notification-app="/" data-notification-owner="account-b"></div>'
        }));
        const unexpectedRequests = [];
        page.on('request', request => {
            if (request.isNavigationRequest() || new URL(request.url()).pathname === '/unexpected-navigation')
                unexpectedRequests.push(request.url());
        });
        await page.route('**/unexpected-navigation', route => route.fulfill({
            contentType: 'text/html', body: '<p>Unexpected navigation</p>'
        }));

        try {
            const started = page.waitForRequest('**/operations/pending');
            await page.getByRole('button', { name: 'Start delayed operation' }).click();
            await started;
            await page.getByRole('button', { name: 'Change account' }).click();
            await expect(page.locator('[data-notification-context]')).toHaveAttribute('data-notification-owner', 'account-b');
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
    await page.route('**/operations/pending', route => route.fulfill({
        contentType: 'text/html', body: '<p>Operation completed</p>',
        headers: { 'HX-Trigger': JSON.stringify({ showToast: { type: 'success', message: 'Profile updated' } }) }
    }));
    await page.getByRole('button', { name: 'Start delayed operation' }).click();

    await expect(page.locator('#operation-result')).toHaveText('Operation completed');
    await expect(page.locator('.toast-body')).toHaveText('Profile updated');
    await expect(page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true })).toBeVisible();
});

test('toasts retain notification metadata and deduplicate history by source identity', async ({ page }) => {
    const input = {
        type: 'error', title: 'Save failed', message: 'Choose another name.',
        sourceId: 'save-profile:one', problemType: 'urn:sample:problem:duplicate-name',
        status: 409, requestId: 'profile-1', operation: 'Save profile', target: 'profile', persist: false
    };
    const entries = await page.evaluate(input => {
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

for (const removal of ['timeout', 'close button', 'HTMX cleanup']) {
    test(`toast ${removal} preserves history and its persisted copy`, async ({ page }) => {
        const entries = await page.evaluate(removal => {
            window.DRN.Toast.show({ message: 'Export completed', duration: removal === 'timeout' ? 1000 : 30_000 });
            return window.DRN.Notifications.list();
        }, removal);
        expect(entries).toHaveLength(1);
        await expect(page.locator('.toast')).toBeVisible();

        if (removal === 'close button') {
            await page.locator('.toast').getByRole('button', { name: 'Close', exact: true }).click();
        } else if (removal === 'HTMX cleanup') {
            await page.evaluate(() => document.dispatchEvent(new CustomEvent('htmx:beforeCleanupElement', {
                detail: { elt: document.querySelector('.toast-container') }
            })));
        }

        await expect(page.locator('.toast')).toHaveCount(0);
        expect(await page.evaluate(() => window.DRN.Notifications.list())).toEqual(entries);
        await expect(page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true })).toBeVisible();

        await page.reload();
        await expect(page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true })).toBeVisible();
        expect(await page.evaluate(() => window.DRN.Notifications.list())).toEqual(entries);
        await expect(page.locator('.toast')).toHaveCount(0);
    });
}

test('visible toast overflow preserves every history entry', async ({ page }) => {
    const entries = await page.evaluate(() => {
        for (const message of ['First', 'Second', 'Third', 'Fourth'])
            window.DRN.Toast.show({ message, duration: 30_000 });
        return window.DRN.Notifications.list();
    });

    expect(entries).toHaveLength(4);
    expect(entries.map(entry => entry.message).sort()).toEqual(['First', 'Fourth', 'Second', 'Third']);
    await expect(page.locator('.toast-body')).toHaveText(['Second', 'Third', 'Fourth']);
    await expect(page.getByRole('button', { name: 'Notifications, 4 unseen', exact: true })).toBeVisible();
    await page.reload();
    await expect(page.getByRole('button', { name: 'Notifications, 4 unseen', exact: true })).toBeVisible();
    expect(await page.evaluate(() => window.DRN.Notifications.list())).toEqual(entries);
});

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
        const entry = window.DRN.Notifications.list()[0];
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

test('prefixed field errors preserve input, render public text safely and clear only their own validation', async ({ page }) => {
    const message = '<img src=x> is not a valid name.';
    await page.route('**/operations/profile', route => route.fulfill({
        status: 422, contentType: 'application/problem+json',
        body: JSON.stringify({ title: 'Profile could not be saved', errors: { 'Input.Name': [message] }, requestId: 'profile-1' })
    }));
    await page.route('**/operations/preferences', route => route.fulfill({
        status: 409, contentType: 'application/problem+json',
        body: JSON.stringify({ title: 'Review your preferences', errors: { '': ['Preferences changed elsewhere'] } })
    }));
    const profile = page.getByRole('form', { name: 'Profile', exact: true });
    const name = page.getByRole('textbox', { name: 'Display name', exact: true });
    const summary = profile.getByRole('region', { name: 'Request error' });
    await name.fill('My unsaved name');
    await page.getByRole('button', { name: 'Save profile', exact: true }).click();

    await expect(summary).toBeFocused();
    await expect(name).toHaveValue('My unsaved name');
    await expect(name).toHaveAttribute('aria-invalid', 'true');
    await expect(name).toHaveAccessibleDescription(`Choose a public display name. Existing validation hint ${message}`);
    await expect(profile.locator('img')).toHaveCount(0);
    const errorLink = summary.getByRole('button', { name: `Input.Name: ${message}`, exact: true });
    await errorLink.focus();
    await errorLink.press('Enter');
    await expect(name).toBeFocused();
    await expect(page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true })).toBeVisible();

    await page.getByRole('button', { name: 'Save preferences', exact: true }).click();
    const otherSummary = page.getByRole('form', { name: 'Preferences', exact: true })
        .getByRole('region', { name: 'Request error' });
    await expect(otherSummary).toContainText('Preferences changed elsewhere');

    await page.unroute('**/operations/profile');
    await page.route('**/operations/profile', route => route.fulfill({
        contentType: 'text/html', body: '<p>Profile saved</p>'
    }));
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
    test(`HTML validation ${permitted ? 'swaps with explicit permission' : 'does not swap without permission'}`, async ({ page }) => {
        const message = 'Correct the profile name.';
        await page.route('**/operations/profile', route => route.fulfill({
            status: 422, contentType: 'text/html',
            headers: permitted ? { 'X-DRN-Validation': 'true' } : {},
            body: `<p class="text-danger">${message}</p>`
        }));
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
    await messages.evaluate(control => {
        control.dataset.actionbarAttentionVersion = 'new-message';
        document.dispatchEvent(new Event('htmx:load'));
    });
    await expect(messages).toHaveClass(/text-bg-warning/);
    await messages.click();
    await expect(panel).toBeVisible();
    await expect(messages).not.toHaveClass(/text-bg-warning/);
    await panel.getByRole('button', { name: 'Close messages' }).click();
    await expect(panel).toBeHidden();
    await page.locator('[data-notification-context]').evaluate(marker => {
        marker.dataset.notificationOwner = 'account-b';
        document.dispatchEvent(new Event('htmx:load'));
    });
    await expect(messages).toHaveClass(/text-bg-warning/);
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
    await expect(page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true })).toHaveClass(/text-bg-warning/);
    await expect(page.locator('.toast')).toHaveCount(0);
    await other.evaluate(() => window.DRN.Notifications.clear());
    await expect(page.locator('[data-notification-count]')).toBeHidden();

    // An already-open tab also receives attendance updates for the same activity revision.
    for (const tab of [page, other]) {
        await tab.getByRole('button', { name: 'Messages', exact: true }).evaluate(control => {
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
    const badge = page.locator('[data-notification-count]');
    await expect(page.getByRole('button', { name: 'Notifications', exact: true })).toBeVisible();
    await expect(badge).toBeHidden();
    await page.route('**/operations/pending', route => route.fulfill({
        status: 400, contentType: 'application/problem+json',
        body: JSON.stringify({ title: 'Bad Request', requestId: 'request-one' })
    }));
    const operation = page.getByRole('button', { name: 'Start delayed operation' });
    await operation.click();
    await expect(page.locator('.toast-body')).toHaveText('Please try again. If the problem continues, contact support and share the error details from Notifications.');
    await expect(page.locator('[data-drn-problem-summary]')).toHaveCount(0);
    await expect(page.locator('#operation-result')).toHaveText('Original result');
    await expect(operation).toBeFocused();
    await expect(badge).toHaveText('1');
    await expect(page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true })).toHaveClass(/text-bg-warning/);
    await expect(badge).toHaveClass(/text-bg-danger/);
    const entries = await page.evaluate(() => window.DRN.Notifications.list());
    expect(entries).toHaveLength(1);
    expect(entries[0]).toMatchObject({ seen: false, status: 400, requestId: 'request-one' });

    await page.getByRole('button', { name: 'Notifications, 1 unseen', exact: true }).click();
    const panel = page.getByRole('dialog', { name: 'Notifications', exact: true });
    await expect(panel).toBeVisible();
    await expect(badge).toBeHidden();
    await expect(panel).toContainText('Please try again. If the problem continues, contact support and share the error details from Notifications.');
    await expect(page.getByRole('button', { name: 'Notifications', exact: true })).not.toHaveClass(/text-bg-warning/);
    await panel.locator('summary').click();
    await expect(panel).toContainText('Share this reference with support so they can find the request.');
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
        window.DRN.Notifications.restore(Array.from({ length: 101 }, (_, index) => ({
            id: `entry-${index}`, type: 'error', title: 'Save failed',
            createdAt: now - index, requestId: `reference-${index}`
        })));
    });
    await page.getByRole('button', { name: 'Notifications, 101 unseen', exact: true }).click();
    const panel = page.getByRole('dialog', { name: 'Notifications', exact: true });
    await panel.getByRole('searchbox', { name: 'Search notifications' }).click();
    const summary = panel.locator('summary').filter({ hasText: '101 occurrences and references' });
    await summary.focus();
    await summary.press('Enter');
    const rows = panel.locator('details[open] > ul > li');
    await expect(rows).toHaveCount(50);
    await expect(summary).toBeFocused();

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
