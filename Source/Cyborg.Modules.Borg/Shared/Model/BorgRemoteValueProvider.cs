using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Modules.Borg.Shared.Model;

public sealed class BorgRemoteValueProvider() : DynamicValueProviderBase<BorgRemote>("cyborg.types.borg.remote.v1.4");
