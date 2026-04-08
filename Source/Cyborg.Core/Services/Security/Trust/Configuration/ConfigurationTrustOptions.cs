using Cyborg.Core.Configuration.Model;

namespace Cyborg.Core.Services.Security.Trust.Configuration;

public sealed record ConfigurationTrustOptions(IReadOnlyList<DynamicValue> Policies);