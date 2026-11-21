// SPDX-License-Identifier: MIT
// Fake DNS-over-HTTPS answers so the E2E tests never depend on live DNS.
import type { Page } from '@playwright/test';

type Zone = Record<string, Record<number, string[]>>;

export const zone: Zone = {
  'example.com': {
    15: ['10 mail.example.com.'],
    16: ['"v=spf1 include:_spf.mailer.example.net mx -all"'],
    1: ['192.0.2.10'],
    2: ['ns1.example.net.', 'ns2.example.net.'],
  },
  'mail.example.com': { 1: ['192.0.2.21'] },
  '_spf.mailer.example.net': { 16: ['"v=spf1 include:_spf2.mailer.example.net ip4:198.51.100.0/24 ~all"'] },
  '_spf2.mailer.example.net': { 16: ['"v=spf1 ip6:2001:db8::/32 ~all"'] },
  'selector1._domainkey.example.com': { 16: ['"v=DKIM1; k=rsa; p=MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAxNHm0GViRtL6QKa6ewJalGJsJ/9GgKw37DskCVO0AG0DUeia6qNohS2MsI2om6g/bT1WyeyqADLNqAMhzKGJZAJMkzsZkzqrhPx/dAf9ACg/kbTcQrsyMeEkzQbQYWdJF0EionfxKy+HDURDW6XroXV7Ldlz74ujnKGY8WpoxUwTtT2OufDxEt20LURCuhUYOfDRlnNdoOGhAYaZ/JDAIVZ3iPeb3hImj8KDaD9id7mWbEcoGsjuX7KHy0lKasSnQiiUTyr5OO399WAvtO28lYsXG0ryOoG07jkNJB0d+U7vdvyjoMNwNQJ9dp7RW7R/2g12mZpaOCQ0R/ys/5m9OQIDAQAB"'] },
  '_dmarc.example.com': { 16: ['"v=DMARC1; p=none; rua=mailto:dmarc@example.com"'] },
};

export async function mockDns(page: Page, extra: Zone = {}) {
  const data: Zone = { ...zone, ...extra };
  await page.route(/https:\/\/(cloudflare-dns\.com|dns\.google)\/.*/, async route => {
    const url = new URL(route.request().url());
    const name = (url.searchParams.get('name') ?? '').replace(/\.$/, '').toLowerCase();
    const type = Number(url.searchParams.get('type'));
    const known = data[name];
    const answers = known?.[type] ?? [];
    const body = {
      Status: known ? 0 : 3,
      Answer: answers.map(d => ({ name: name + '.', type, TTL: 300, data: d })),
    };
    await route.fulfill({ status: 200, contentType: 'application/dns-json', headers: { 'access-control-allow-origin': '*' }, body: JSON.stringify(body) });
  });
}
