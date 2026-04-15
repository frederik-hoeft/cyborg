using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Core.Services.Security.Trust.Policies;

public sealed class UnixOwnerTrustPolicyDynamicProvider() : DynamicValueProviderBase<UnixOwnerTrustPolicy>("cyborg.trust.policy.unix.owner");