// Production service worker: precaches all app assets, caches CDN resources on
// first hit, passes AI provider calls straight to the network, and serves
// index.html for all navigation requests so client-side routing keeps working.

self.importScripts('./service-worker-assets.js');

const CACHE_NAME = 'sja-' + self.assetsManifest.version;
const CDN_CACHE_NAME = 'sja-cdn-v1';

const PRECACHE_INCLUDE = [
    /\.dll$/, /\.pdb$/, /\.wasm/,
    /\.html$/, /\.js$/, /\.json$/, /\.css$/,
    /\.woff2?$/, /\.ttf$/,
    /\.png$/, /\.ico$/, /\.svg$/, /\.webp$/,
    /\.blat$/, /\.dat$/,
    /\.webmanifest$/,
];
const PRECACHE_EXCLUDE = [/^_framework\/blazor\.server\.js/];

// CDN hosts whose responses are cached after the first network hit.
const CDN_HOSTS = ['cdnjs.cloudflare.com', 'cdn.jsdelivr.net'];

self.addEventListener('install', event => event.waitUntil(onInstall()));
self.addEventListener('activate', event => event.waitUntil(onActivate()));
self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') return;
    event.respondWith(onFetch(event));
});
self.addEventListener('message', event => {
    if (event.data?.type === 'SKIP_WAITING') self.skipWaiting();
});

async function onInstall() {
    const cache = await caches.open(CACHE_NAME);
    const requests = self.assetsManifest.assets
        .filter(a => PRECACHE_INCLUDE.some(p => p.test(a.url)))
        .filter(a => !PRECACHE_EXCLUDE.some(p => p.test(a.url)))
        .map(a => new Request(a.url, { integrity: a.hash, cache: 'no-cache' }));
    await cache.addAll(requests);
    // Do NOT skipWaiting here — the update banner lets the user decide when to reload.
}

async function onActivate() {
    const keys = await caches.keys();
    await Promise.all(
        keys.filter(k => k !== CACHE_NAME && k !== CDN_CACHE_NAME)
            .map(k => caches.delete(k))
    );
    await self.clients.claim();
}

async function onFetch(event) {
    const url = new URL(event.request.url);

    // CDN resources: cache-first, populate on first network hit.
    if (CDN_HOSTS.includes(url.hostname)) {
        const cached = await caches.match(event.request);
        if (cached) return cached;
        try {
            const response = await fetch(event.request);
            if (response.ok) {
                caches.open(CDN_CACHE_NAME).then(c => c.put(event.request, response.clone()));
            }
            return response;
        } catch {
            return Response.error();
        }
    }

    // Any other cross-origin request (AI providers, etc.): network only.
    if (url.origin !== self.location.origin) {
        return fetch(event.request);
    }

    // Same-origin: serve from precache.
    // Navigation requests map to index.html so the Blazor router handles them.
    const request = event.request.mode === 'navigate'
        ? new Request('index.html')
        : event.request;

    const cached = await caches.match(request);
    if (cached) return cached;

    try {
        return await fetch(event.request);
    } catch {
        return new Response('Offline', { status: 503, statusText: 'Service Unavailable' });
    }
}
