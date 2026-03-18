using Cyborg.Core.Modules.Configuration.Serialization.Dynamics.ValueProviders;

namespace Cyborg.Modules.Borg.Shared.Model;

public sealed class BorgRemoteRepositoryValueProvider() : DynamicValueProviderBase<BorgRemoteRepository>("cyborg.types.borg.repository.v1");