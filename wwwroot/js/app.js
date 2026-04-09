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
