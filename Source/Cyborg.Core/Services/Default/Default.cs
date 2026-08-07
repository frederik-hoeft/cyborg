using Cyborg.Core.Configuration;

namespace Cyborg.Core.Services.Default;

public sealed class Default<TService>(IServiceSelectionKey<TService> serviceKey, IEnumerable<TService> services, IConfiguration configuration)
    : IDefault<TService> where TService : class, IKeyedService
{
    public TService? GetDefault()
    {
        if (!configuration.TryGetValue(serviceKey.Key, out string? selectedService))
        {
            selectedService = serviceKey.DefaultService;
        }
        if (selectedService is null)
        {
            return null;
        }
        return services.FirstOrDefault(s => s.Key.Equals(selectedService, StringComparison.OrdinalIgnoreCase));
    }

    public TService GetRequiredDefault() =>
        GetDefault() ?? throw new InvalidOperationException($"No registered service matches the configured or default selection for key '{serviceKey.Key}'.");
}
