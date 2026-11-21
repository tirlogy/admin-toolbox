// SPDX-License-Identifier: MIT
// Produces the README and portfolio screenshots (uploaded as CI artifact "screenshots").
import { expect, test } from '@playwright/test';
import { mockDns } from './dns-mock';
import { waitForBlazor } from './helpers';

const out = (name: string) => `screenshots/${name}.png`;

test.describe('screenshots', () => {
  test.use({ viewport: { width: 1280, height: 900 }, deviceScaleFactor: 1 });

  test('capture', async ({ page }) => {
    await mockDns(page);
    await page.goto('/');
    await expect(page.locator('.tool-grid')).toBeVisible();
    await page.screenshot({ path: out('home') });

    await page.goto('/mail');
    await waitForBlazor(page);
    await page.getByLabel('Domain', { exact: true }).fill('example.com');
    await page.getByRole('button', { name: 'Check' }).click();
    await expect(page.getByText('DNS lookups: 3 of 10')).toBeVisible();
    await page.locator('#check-Spf').scrollIntoViewIfNeeded();
    await page.screenshot({ path: out('mail-check'), fullPage: false });

    await page.goto('/header');
    await waitForBlazor(page);
    await page.getByRole('button', { name: 'Load example' }).click();
    await page.locator('h2', { hasText: 'Route' }).scrollIntoViewIfNeeded();
    await page.screenshot({ path: out('header-analysis') });

    await page.goto('/subnet');
    await waitForBlazor(page);
    await page.getByLabel('Network or address').fill('2001:db8:abcd::/48');
    await page.getByRole('button', { name: 'Calculate' }).click();
    await expect(page.getByRole('heading', { name: '2001:db8:abcd::/48' })).toBeVisible();
    await page.screenshot({ path: out('subnet') });

    await page.emulateMedia({ colorScheme: 'dark' });
    await page.goto('/helpers/password');
    await waitForBlazor(page);
    await page.screenshot({ path: out('helpers-dark') });
  });
});
