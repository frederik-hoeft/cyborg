using Cyborg.Core.Modules.Configuration.Serialization.Dynamics.ValueProviders;

namespace Cyborg.Modules.Borg.Model;

public sealed class BorgRemoteValueProvider() : DynamicValueProviderBase<BorgRemote>("cyborg.types.borg.remote.v1");
