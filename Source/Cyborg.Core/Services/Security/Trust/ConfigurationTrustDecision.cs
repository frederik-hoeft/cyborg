namespace Cyborg.Core.Services.Security.Trust;

public sealed record ConfigurationTrustDecision(string Path, bool IsTrusted, IReadOnlyList<ConfigurationTrustPolicyDecision> Decisions);
