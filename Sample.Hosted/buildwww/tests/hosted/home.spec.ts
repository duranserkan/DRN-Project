import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

test('rejecting optional cookies persists the choice across a Razor page reload', async ({ page }) => {
    await page.goto('./');
    const footer = page.locator('[data-js-drn-footer]');
    const banner = footer.getByRole('alert');
    await banner.getByRole('button', { name: 'Reject' }).click();
    await expect(banner).toBeHidden();
    await expect(footer.getByRole('navigation')).toBeVisible();
    await page.reload();
    await expect(footer).toHaveAttribute('data-show-cookie-banner', 'false');
    await expect(banner).toBeHidden();
    await footer.getByRole('button', { name: 'Preference Manager' }).click();
    for (const name of ['Analytics Cookies', 'Marketing Cookies']) {
        const choice = footer.getByRole('checkbox', { name });
        await expect(choice).toBeVisible();
        await expect(choice).not.toBeChecked();
    }
});

test('anonymous Razor home page has no detected accessibility violations after React mounts', async ({ page }) => {
    const response = await page.goto('./');
    expect(response?.ok()).toBe(true);
    await expect(page.getByRole('heading', { name: 'Welcome to DRN Framework', level: 1 })).toBeVisible();
    const island = page.locator('[data-js-hello-react-island]');
    await expect(island.getByRole('heading', { name: 'Build Better, Ship Faster' })).toBeVisible();

    const scan = await new AxeBuilder({ page }).analyze();
    expect(scan.violations).toEqual([]);
});
