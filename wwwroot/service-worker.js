// Development stub — caching disabled to preserve hot-reload behavior.
// In production, this file is replaced by service-worker.published.js.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
