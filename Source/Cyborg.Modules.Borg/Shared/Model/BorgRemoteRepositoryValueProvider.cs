using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Modules.Borg.Shared.Model;

public sealed class BorgRemoteRepositoryValueProvider() : DynamicValueProviderBase<BorgRemoteRepository>("cyborg.types.borg.repository.v1.4");