// SPDX-License-Identifier: MIT
// Offline support: caches all published assets and serves index.html for navigations.
// Based on the Blazor PWA template (https://aka.ms/blazor-offline-considerations).
self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall()));
self.addEventListener('activate', event => event.waitUntil(onActivate()));
self.addEventListener('fetch', event => {
  // Only same-origin GET requests are served from the cache. DNS-over-HTTPS calls go straight to the network.
  if (event.request.method !== 'GET' || new URL(event.request.url).origin !== self.location.origin) return;
  event.respondWith(onFetch(event));
});

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff2?$/, /\.png$/, /\.svg$/, /\.ico$/, /\.dat$/, /\.webmanifest$/];
const offlineAssetsExclude = [/^service-worker\.js$/];
const baseUrl = new URL(self.registration.scope);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall() {
  const requests = self.assetsManifest.assets
    .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
    .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
    .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
  const cache = await caches.open(cacheName);
  await cache.addAll(requests);
  await self.skipWaiting();
}

async function onActivate() {
  const keys = await caches.keys();
  await Promise.all(keys.filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName).map(key => caches.delete(key)));
  await self.clients.claim();
}

async function onFetch(event) {
  const shouldServeIndexHtml = event.request.mode === 'navigate' && !manifestUrlList.some(url => url === event.request.url);
  const request = shouldServeIndexHtml ? 'index.html' : event.request;
  const cache = await caches.open(cacheName);
  const cached = await cache.match(request);
  // Cloudflare Pages answers index.html with a 308 to /. Browsers reject a redirected response
  // for a navigation (ERR_FAILED), so hand out a clean copy without the redirect flag.
  if (cached && cached.redirected) {
    return new Response(cached.body, { status: cached.status, statusText: cached.statusText, headers: cached.headers });
  }
  return cached || fetch(event.request);
}
