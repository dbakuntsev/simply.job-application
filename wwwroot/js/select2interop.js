// Select2 interop for the TagPicker Blazor component.
// Requires jQuery and Select2 4.x to be loaded before this script.

window.sjaSelect2Init = function (element, availableOptions, selectedValues, allowCreate, dotNetRef) {
    if (!element || typeof $ === 'undefined') return;

    // Clear any existing children
    while (element.firstChild) element.removeChild(element.firstChild);

    // Build option set: union of available lookup values and current selected values
    // (selected values may include custom labels not yet in the lookup)
    var allOptions = Array.from(new Set(availableOptions.concat(selectedValues)));
    allOptions.forEach(function (opt) {
        var isSelected = selectedValues.indexOf(opt) >= 0;
        var option = new Option(opt, opt, isSelected, isSelected);
        element.appendChild(option);
    });

    $(element).select2({
        tags: allowCreate,
        width: '100%',
        placeholder: 'Add roles\u2026',
        allowClear: false,
        theme: 'default',
    });

    $(element).on('change.sja', function () {
        var values = Array.from($(element).val() || []);
        dotNetRef.invokeMethodAsync('OnTagsChangedFromJs', values);
    });
};

window.sjaSelect2Destroy = function (element) {
    if (!element || typeof $ === 'undefined') return;
    try {
        if ($(element).data('select2')) {
            $(element).off('change.sja');
            $(element).select2('destroy');
        }
    } catch (e) { }
};
