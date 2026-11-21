// SPDX-License-Identifier: MIT
import { expect, test } from '@playwright/test';
import { mockDns } from './dns-mock';
import { trackErrors, waitForBlazor } from './helpers';

const PEM = `-----BEGIN CERTIFICATE-----
MIICljCCAjygAwIBAgIJAImkh1EYvln7MAoGCCqGSM49BAMCMD8xCzAJBgNVBAYT
AkRFMRUwEwYDVQQKDAxFeGFtcGxlIFRlc3QxGTAXBgNVBAMMEHd3dy5leGFtcGxl
LnRlc3QwHhcNMjYxMTIxMTEzNDA5WhcNMzYxMTE4MTEzNDA5WjA/MQswCQYDVQQG
EwJERTEVMBMGA1UECgwMRXhhbXBsZSBUZXN0MRkwFwYDVQQDDBB3d3cuZXhhbXBs
ZS50ZXN0MIIBSzCCAQMGByqGSM49AgEwgfcCAQEwLAYHKoZIzj0BAQIhAP////8A
AAABAAAAAAAAAAAAAAAA////////////////MFsEIP////8AAAABAAAAAAAAAAAA
AAAA///////////////8BCBaxjXYqjqT57PrvVV2mIa8ZR0GsMxTsPY7zjw+J9Jg
SwMVAMSdNgiG5wSTamZ44ROdJreBn36QBEEEaxfR8uEsQkf4vOblY6RA8ncDfYEt
6zOg9KE5RdiYwpZP40Li/hp/m47n60p8D54WK84zV2sxXs7LtkBoN79R9QIhAP//
//8AAAAA//////////+85vqtpxeehPO5ysL8YyVRAgEBA0IABDGL/lJTRC499vHa
6J+3+ATzq78lbRmp9LE+gXWqbHBVdZi+3bKmOPOp+itxeyzLVHqj2l4Cij4wHVyc
DIxtJb6jLTArMCkGA1UdEQQiMCCCEHd3dy5leGFtcGxlLnRlc3SCDGV4YW1wbGUu
dGVzdDAKBggqhkjOPQQDAgNIADBFAiEAjUyIORlsoghS+gn19J37R5jRLc5FWf/R
52knwNYOZDUCIGwjKLsmmKvFG/q04EAxoKG9pUZD7BubJ8lIL/WF3Q2L
-----END CERTIFICATE-----`;

test('DNS diff detects missing, changed and new records', async ({ page }) => {
  await mockDns(page);
  await page.goto('/dns-diff');
  await waitForBlazor(page);
  await page.getByRole('button', { name: 'Load example' }).click();
  await page.getByRole('button', { name: 'Compare' }).click();
  const table = page.locator('table');
  await expect(table).toBeVisible();
  const row = (name: string, type: string) => table.locator('tbody tr')
    .filter({ has: page.locator('td:nth-child(1)', { hasText: new RegExp(`^${name.replace(/\./g, '\\.')}$`) }) })
    .filter({ has: page.locator('td:nth-child(2)', { hasText: new RegExp(`^${type}$`) }) });
  await expect(row('example.com', 'MX')).toContainText('unchanged');
  await expect(row('example.com', 'TXT')).toContainText('changed');
  await expect(row('_dmarc.example.com', 'TXT')).toContainText('changed');
  await expect(row('mail.example.com', 'A')).toContainText('changed');
  await expect(row('www.example.com', 'CNAME')).toContainText('missing');
});

test('helpers work: hash, password, JWT, encoder and certificate', async ({ page }) => {
  const errors = trackErrors(page);
  await page.goto('/helpers/hash');
  await waitForBlazor(page);
  await page.getByLabel('Text', { exact: true }).fill('abc');
  await expect(page.locator('[data-hash="Sha256"]')).toHaveText('ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad');
  await expect(page.locator('[data-hash="Md5"]')).toHaveText('900150983cd24fb0d6963f7d28e17f72');

  await page.getByRole('link', { name: 'Password generator' }).click();
  await expect(page.locator('#pw-output')).toHaveValue(/^.{20}$/);

  await page.getByRole('link', { name: 'JWT decoder' }).click();
  await page.getByLabel('Token').fill('eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiI0MiIsImV4cCI6MTB9.c2ln');
  await expect(page.getByText('Expired.')).toBeVisible();

  await page.getByRole('link', { name: 'Encoder' }).click();
  await page.getByLabel('Input').fill('Grüße');
  await page.getByRole('button', { name: 'Encode', exact: true }).click();
  await expect(page.locator('#enc-output')).toHaveValue('R3LDvMOfZQ==');

  await page.getByRole('link', { name: 'Certificate' }).click();
  await page.getByLabel('Certificate (PEM)').fill(PEM);
  await expect(page.getByText('DNS:www.example.test, DNS:example.test')).toBeVisible();
  await expect(page.getByText(/ecdsa-with-SHA256/)).toBeVisible();
  expect(errors).toEqual([]);
});

test('German and Turkish user interface', async ({ page }) => {
  await page.goto('/');
  await page.evaluate(() => localStorage.setItem('atb.lang', 'de'));
  await page.goto('/subnet');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Subnetzrechner');
  await expect(page.locator('html')).toHaveAttribute('lang', 'de');
  await page.locator('#lang-select').selectOption('tr');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Alt ağ hesaplayıcı');
  await page.goto('/helpers/hash');
  await expect(page.getByRole('columnheader', { name: 'Algoritma' })).toBeVisible();
});

test('helpers keep working offline (PWA)', async ({ page, context }) => {
  await page.goto('/helpers/hash');
  await waitForBlazor(page);
  await page.evaluate(async () => {
    const reg = await navigator.serviceWorker.ready;
    if (!navigator.serviceWorker.controller) {
      await new Promise(r => navigator.serviceWorker.addEventListener('controllerchange', r, { once: true }));
    }
    return reg.active?.state;
  });
  await context.setOffline(true);
  await page.reload();
  await waitForBlazor(page);
  await page.getByLabel('Text', { exact: true }).fill('abc');
  await expect(page.locator('[data-hash="Sha1"]')).toHaveText('a9993e364706816aba3e25717850c26c9cd0d89d');
  await context.setOffline(false);
});

test('PWA is installable (Chrome installability check)', async ({ page }) => {
  await page.goto('/helpers');
  await waitForBlazor(page);
  await page.evaluate(() => navigator.serviceWorker.ready);
  const cdp = await page.context().newCDPSession(page);
  const { installabilityErrors } = await cdp.send('Page.getInstallabilityErrors');
  expect(installabilityErrors).toEqual([]);
  const manifest = await page.evaluate(async () => (await fetch('manifest.webmanifest')).json());
  expect(manifest.icons.map((i: { sizes: string }) => i.sizes)).toEqual(expect.arrayContaining(['192x192', '512x512']));
});
