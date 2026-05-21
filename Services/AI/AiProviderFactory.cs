using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Simply.JobApplication.Services.AI.OpenAi;

namespace Simply.JobApplication.Services.AI;

public class AiProviderFactory : IAiProviderFactory
{
    private readonly Dictionary<string, IAiProvider> _providers;

    public AiProviderFactory(IServiceProvider serviceProvider)
    {
        var openAi = serviceProvider.GetRequiredService<OpenAiProvider>();
        _providers = new Dictionary<string, IAiProvider>
        {
            [openAi.ProviderId] = openAi,
        };
    }

    public IReadOnlyList<IAiProvider> All => _providers.Values.ToList();

    public IAiProvider Get(string providerId) =>
        _providers.TryGetValue(providerId, out var p)
            ? p
            : _providers["openai"];
}
