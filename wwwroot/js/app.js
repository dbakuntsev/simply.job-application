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
