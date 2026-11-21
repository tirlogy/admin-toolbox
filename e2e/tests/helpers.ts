// SPDX-License-Identifier: MIT
import { expect, type Page } from '@playwright/test';

/** Collects console errors (including CSP violations) and page errors. */
export function trackErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on('console', msg => {
    if (msg.type() === 'error') errors.push(msg.text());
  });
  page.on('pageerror', err => errors.push(err.message));
  return errors;
}

/** Waits until Blazor has replaced the static shell (the language selector only exists in Blazor). */
export async function waitForBlazor(page: Page) {
  await expect(page.locator('#lang-select')).toBeVisible();
}
