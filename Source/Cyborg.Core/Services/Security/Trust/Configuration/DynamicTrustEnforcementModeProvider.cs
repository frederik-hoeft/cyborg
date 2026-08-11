using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Core.Services.Security.Trust.Configuration;

public sealed class DynamicTrustEnforcementModeProvider() : DynamicEnumValueProvider<TrustEnforcementMode>("cyborg.types.services.trust.enforcement_mode.v1");
