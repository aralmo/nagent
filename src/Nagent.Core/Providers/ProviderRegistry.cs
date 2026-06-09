namespace Nagent.Core.Providers;

public sealed class ProviderRegistry
{
    private readonly Dictionary<string, IModelProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public ProviderRegistry(IEnumerable<IModelProvider> providers)
    {
        foreach (var provider in providers)
        {
            _providers[provider.ProviderName] = provider;
        }
    }

    public IModelProvider GetProvider(string name)
    {
        if (_providers.TryGetValue(name, out var provider))
        {
            return provider;
        }

        throw new ProviderException($"Unknown provider '{name}'.");
    }
}
