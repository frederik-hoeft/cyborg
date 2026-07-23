using Cyborg.Core;
using Cyborg.Modules.Borg;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cyborg.Modules.Tests.Infrastructure;

/// <summary>
/// Builds the default <see cref="IServiceCollection"/> for module tests by reflecting over the Jab
/// service provider module interfaces used in production (<see cref="ICyborgCoreServices"/>,
/// <see cref="ICyborgModuleServices"/>, <see cref="ICyborgBorgServices"/>).
/// </summary>
/// <remarks>
/// The resulting service collection mirrors the production DI graph but uses MEDI's runtime registration
/// model instead of Jab's compile-time code generation. This is acceptable because the test project does not
/// require AOT compatibility. Callers may override or extend the registrations before the service provider
/// is built.
/// </remarks>
internal static class TestServiceConfiguration
{
    /// <summary>
    /// Creates an <see cref="IServiceCollection"/> pre-populated with all services from the production
    /// Jab modules, plus a default <see cref="ILoggerFactory"/> registration.
    /// </summary>
    /// <returns>A mutable service collection ready for per-test-class or per-test-case customization.</returns>
    public static IServiceCollection CreateDefaultServices()
    {
        ServiceCollection services = new();

        // Register all production Jab modules via reflection
        JabRegistrationDiscovery.RegisterFromJabModule<ICyborgCoreServices>(services);
        JabRegistrationDiscovery.RegisterFromJabModule<ICyborgModuleServices>(services);
        JabRegistrationDiscovery.RegisterFromJabModule<ICyborgBorgServices>(services);

        // Provide a default (silent) logger factory — tests that need logging can override this.
        // The logger factory is registered as a singleton and will be disposed by the service provider.
        services.AddSingleton<ILoggerFactory>(_ => LoggerFactory.Create(static _ => { }));

        return services;
    }
}