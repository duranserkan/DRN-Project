import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

test('anonymous Razor home page has no detected accessibility violations after React mounts', async ({ page }) => {
    const response = await page.goto('./');
    expect(response?.ok()).toBe(true);
    await expect(page.getByRole('heading', { name: 'Welcome to DRN Framework', level: 1 })).toBeVisible();
    const island = page.locator('[data-js-hello-react-island]');
    await expect(island.getByRole('heading', { name: 'Build Better, Ship Faster' })).toBeVisible();

    const scan = await new AxeBuilder({ page }).analyze();
    expect(scan.violations).toEqual([]);
});
