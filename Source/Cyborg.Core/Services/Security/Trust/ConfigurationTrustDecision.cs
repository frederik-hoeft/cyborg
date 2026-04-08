namespace Cyborg.Core.Services.Security.Trust;

public sealed record ConfigurationTrustDecision(bool IsTrusted, IReadOnlyList<ConfigurationTrustPolicyDecision> Decisions);