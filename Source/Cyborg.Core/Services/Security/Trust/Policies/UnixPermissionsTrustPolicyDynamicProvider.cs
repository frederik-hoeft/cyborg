using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Core.Services.Security.Trust.Policies;

public sealed class UnixPermissionsTrustPolicyDynamicProvider() : DynamicValueProviderBase<UnixPermissionsTrustPolicy>("cyborg.trust.policy.unix.permissions");
