using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Core.Services.Security.Trust.Configuration;

public sealed class ConfigurationTrustOptionsDynamicProvider() : DynamicValueProviderBase<ConfigurationTrustOptions>("cyborg.types.services.trust.options.v1");
