// CartStack — installability-only service worker.
// No offline caching: Blazor Server needs the SignalR circuit, and a
// stale grocery list is worse than a missing one. This SW exists only
// so iOS / Android browsers consider the app installable.

self.addEventListener("install", () => {
    self.skipWaiting();
});

self.addEventListener("activate", (event) => {
    event.waitUntil(self.clients.claim());
});

// No fetch handler on purpose — let every request hit the network.
