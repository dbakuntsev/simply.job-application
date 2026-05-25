// Registers / unregisters a beforeunload handler so the browser shows its
// native "Leave site?" prompt when the user closes a tab or navigates away
// while a form is dirty. Call with true to arm, false to disarm.
// Used by NavigationGuardBase as a WASM substitute for ConfirmExternalNavigations
// (which is not available in Blazor WASM 8.x).
(function () {
    let _active = false;
    function _handler(e) { e.preventDefault(); e.returnValue = ''; }
    window.sjaSetBeforeUnload = function (enable) {
        if (enable && !_active)  { window.addEventListener('beforeunload', _handler);    _active = true; }
        if (!enable && _active)  { window.removeEventListener('beforeunload', _handler); _active = false; }
    };
})();

window.sjaGetStorageEstimate = async function () {
    if (!navigator.storage || !navigator.storage.estimate) return null;
    var est = await navigator.storage.estimate();
    return [est.quota ?? 0, est.usage ?? 0];
};

window.sjaScrollTo = function (id) {
    var el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
};

// Returns the locale's first day of week as a JS Date.getDay() index:
// 0 = Sunday, 1 = Monday, ..., 6 = Saturday.
// Intl.Locale.getWeekInfo() / .weekInfo uses ISO numbering (1 = Mon ... 7 = Sun);
// translate back to the JS index. Falls back to Sunday (0) if the locale APIs
// or properties aren't available.
window.sjaGetFirstDayOfWeek = function () {
    try {
        var loc = new Intl.Locale(navigator.language || 'en-US');
        var info = (typeof loc.getWeekInfo === 'function') ? loc.getWeekInfo() : loc.weekInfo;
        var iso = info && info.firstDay;
        if (typeof iso === 'number' && iso >= 1 && iso <= 7) {
            return iso === 7 ? 0 : iso;
        }
    } catch (_) { /* fall through */ }
    return 0;
};

// Hide every open popover. Called on Blazor navigation and before opening a new one.
window.sjaHideAllPopovers = function () {
    document.querySelectorAll('[data-bs-toggle="popover"]').forEach(function (el) {
        var instance = bootstrap.Popover.getInstance(el);
        if (instance) instance.hide();
    });
};

// Close all other popovers whenever a new one is about to open.
document.addEventListener('show.bs.popover', function (e) {
    document.querySelectorAll('[data-bs-toggle="popover"]').forEach(function (el) {
        if (el !== e.target) {
            var instance = bootstrap.Popover.getInstance(el);
            if (instance) instance.hide();
        }
    });
});
