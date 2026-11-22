// SPDX-License-Identifier: MIT
import { expect, test } from '@playwright/test';
import { mockDns } from './dns-mock';
import { trackErrors, waitForBlazor } from './helpers';

test('main flow: home to mail domain check with SPF tree', async ({ page }) => {
  const errors = trackErrors(page);
  await mockDns(page);
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Tools for IT admins');
  await waitForBlazor(page);

  await page.getByRole('navigation', { name: 'Tools' }).getByRole('link', { name: /^(Mail domain check|Mail)$/ }).click(); // phones show the short tab label
  await expect(page).toHaveURL(/\/mail$/);
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Mail domain check');

  await page.getByLabel('Domain', { exact: true }).fill('https://Example.com/contact');
  await page.getByRole('button', { name: 'Check' }).click();

  await expect(page.getByRole('heading', { name: 'Result for example.com' })).toBeVisible();
  await expect(page.getByText('DNS lookups: 3 of 10')).toBeVisible();
  await expect(page.locator('.spf-node code', { hasText: '_spf2.mailer.example.net' })).toBeVisible();
  await expect(page.getByText('Policy p=none: monitoring only, spoofed mail is delivered.')).toBeVisible();
  await expect(page.getByText('No BIMI record (optional).')).toBeVisible();
  await expect(page.locator('.history .chip', { hasText: 'example.com' })).toBeVisible();
  expect(errors).toEqual([]);
});

test('header analysis shows the route as a timeline', async ({ page }) => {
  const errors = trackErrors(page);
  await page.goto('/header');
  await waitForBlazor(page);
  await page.getByRole('button', { name: 'Load example' }).click();
  await expect(page.locator('.timeline > li')).toHaveCount(3);
  await expect(page.getByText('DMARC did not pass (fail).')).toBeVisible();
  await expect(page.getByText(/Replies go to help-desk\.example/)).toBeVisible();
  await expect(page.getByText('Hop 2 took 12 minutes.')).toBeVisible();
  expect(errors).toEqual([]);
});

test('subnet calculator handles split, contains and summarize', async ({ page }) => {
  await page.goto('/subnet');
  await waitForBlazor(page);
  await page.getByLabel('Network or address').fill('10.0.0.0/22');
  await page.getByRole('button', { name: 'Calculate' }).click();
  await expect(page.getByRole('heading', { name: '10.0.0.0/22' })).toBeVisible();
  await expect(page.locator('dd', { hasText: '10.0.0.1 – 10.0.3.254' })).toBeVisible();
  await page.getByLabel('IP address').fill('10.0.3.5');
  await expect(page.getByText('Yes, 10.0.3.5 is in 10.0.0.0/22.')).toBeVisible();
  await page.getByLabel('Networks, one per line').fill('192.168.0.0/24\n192.168.1.0/24');
  await expect(page.locator('pre', { hasText: '192.168.0.0/23' })).toBeVisible();
});
