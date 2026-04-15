using Cyborg.Core.Configuration;
using Cyborg.Core.Services.Security.Trust.Configuration;

namespace Cyborg.Core.Services.Security.Trust;

public sealed class DefaultConfigurationTrustOptionsProvider(IConfiguration configuration) : IConfigurationTrustOptionsProvider
{
    public ConfigurationTrustOptions Options => field ??= configuration.Get("cyborg.services.trust", ConfigurationTrustOptions.Default);
}
