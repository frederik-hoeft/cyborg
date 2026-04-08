using Cyborg.Core.Configuration.Serialization.Dynamics;
using Cyborg.Core.Services.Security.Trust.Configuration;
using Jab;

namespace Cyborg.Core.Services.Security.Trust;

[ServiceProviderModule]
[Singleton<IDynamicValueProvider, ConfigurationTrustOptionsDynamicProvider>]
[Singleton<IConfigurationTrustMonitor, UnionConfigurationTrustMonitor>]
public interface IConfigurationTrustServices;