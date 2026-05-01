// PWA utilities: install prompt, service-worker update detection, standalone detection.
//
// The beforeinstallprompt event is captured early in index.html (before Blazor loads)
// and stored on window.__pwaInstallPrompt. This module reads that global and wires
// up Blazor callbacks so components react when the state changes.

export function isStandalone() {
    return window.matchMedia('(display-mode: standalone)').matches ||
        !!window.navigator.standalone;
}

export function isInstallable() {
    return window.__pwaInstallPrompt != null;
}

export function init(dotnetRef) {
    // The prompt may have fired before this ES module was imported.
    if (window.__pwaInstallPrompt) {
        dotnetRef.invokeMethodAsync('OnInstallPromptAvailable');
    }

    // Wire up callbacks for future events captured by the early inline script.
    window.__pwaInstallPromptCallback = () =>
        dotnetRef.invokeMethodAsync('OnInstallPromptAvailable');

    window.__pwaInstalledCallback = () =>
        dotnetRef.invokeMethodAsync('OnAppInstalled');

    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.ready.then(reg => {
            // A SW may already be waiting when the page loads after a background update.
            if (reg.waiting && navigator.serviceWorker.controller) {
                dotnetRef.invokeMethodAsync('OnUpdateWaiting');
            }
            reg.addEventListener('updatefound', () => {
                const sw = reg.installing;
                sw.addEventListener('statechange', () => {
                    if (sw.state === 'installed' && navigator.serviceWorker.controller) {
                        dotnetRef.invokeMethodAsync('OnUpdateWaiting');
                    }
                });
            });
        });

        // When the new SW takes control (after skipWaiting), reload the page.
        let refreshing = false;
        navigator.serviceWorker.addEventListener('controllerchange', () => {
            if (!refreshing) {
                refreshing = true;
                location.reload();
            }
        });
    }
}

export async function promptInstall() {
    const prompt = window.__pwaInstallPrompt;
    if (!prompt) return false;
    prompt.prompt();
    const { outcome } = await prompt.userChoice;
    if (outcome === 'accepted') window.__pwaInstallPrompt = null;
    return outcome === 'accepted';
}

export async function applyUpdate() {
    const reg = await navigator.serviceWorker.ready;
    if (reg.waiting) reg.waiting.postMessage({ type: 'SKIP_WAITING' });
}
