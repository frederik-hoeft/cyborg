using Cyborg.Core.Modules.Configuration.Serialization.Dynamics.ValueProviders;

namespace Cyborg.Modules.Borg.Shared.Model;

public sealed class BorgRemoteValueProvider() : DynamicValueProviderBase<BorgRemote>("cyborg.types.borg.remote.v1.4");