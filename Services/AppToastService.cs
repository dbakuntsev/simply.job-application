namespace Simply.JobApplication.Services;

public sealed class AppToastService
{
    private readonly List<AppToastMessage> _messages = new();

    public event Action? Changed;

    public IReadOnlyList<AppToastMessage> Messages => _messages;

    public void ShowSuccess(string message) => Show(message, "success");

    public void ShowInfo(string message) => Show(message, "info");

    public void ShowError(string message) => Show(message, "danger");

    public void Dismiss(Guid id)
    {
        var removed = _messages.RemoveAll(t => t.Id == id) > 0;
        if (removed) Changed?.Invoke();
    }

    private void Show(string message, string kind)
    {
        _messages.Add(new AppToastMessage(Guid.NewGuid(), message, kind));
        Changed?.Invoke();
    }
}

public sealed record AppToastMessage(Guid Id, string Message, string Kind);
