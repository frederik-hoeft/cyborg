namespace Cyborg.Core.Services.Security.Trust;

public sealed record ConfigurationTrustPolicyDecision
(
    string PolicyName,
    ConfigurationTrustDecisionKind Decision,
    string? Reason
);