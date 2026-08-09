using Cyborg.Core.Configuration;

namespace Cyborg.Core.Services.Default;

public sealed class Default<TService>(IServiceSelectionKey<TService> serviceKey, IEnumerable<TService> services, IConfiguration configuration)
    : IDefault<TService> where TService : class, IKeyedService
{
    private readonly Dictionary<string, TService> _services = services.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);

    public TService? GetDefault()
    {
        bool configured = true;
        if (!configuration.TryGetValue(serviceKey.Key, out string? selectedService))
        {
            selectedService = serviceKey.DefaultService;
            configured = false;
        }
        if (selectedService is null)
        {
            return null;
        }
        if (_services.TryGetValue(selectedService, out TService? service))
        {
            return service;
        }
        throw new InvalidOperationException($"No registered service matches the {(configured ? "configured" : "default")} selection '{selectedService}' for key '{serviceKey.Key}'.");
    }

    public TService GetRequiredDefault() =>
        GetDefault() ?? throw new InvalidOperationException($"No service is configured and no default service exists for key '{serviceKey.Key}'.");
}
