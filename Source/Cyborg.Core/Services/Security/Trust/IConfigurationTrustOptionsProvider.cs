using Cyborg.Core.Services.Security.Trust.Configuration;

namespace Cyborg.Core.Services.Security.Trust;

public interface IConfigurationTrustOptionsProvider
{
    ConfigurationTrustOptions Options { get; }
}
