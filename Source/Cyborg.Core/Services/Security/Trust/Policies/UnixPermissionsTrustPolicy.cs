using System.Collections.Immutable;

namespace Cyborg.Core.Services.Security.Trust.Policies;

public sealed record UnixPermissionsTrustPolicy(ImmutableArray<UnixFileMode> RequiredBits, ImmutableArray<UnixFileMode> ForbiddenBits) : TrustPolicyBase("cyborg.trust.policy.unix.permissions")
{
    public override ValueTask<ConfigurationTrustPolicyDecision> EvaluateAsync(IServiceProvider serviceProvider, ConfigurationTrustSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (OperatingSystem.IsWindows())
        {
            return ValueTaskOf(new ConfigurationTrustPolicyDecision
            (
                Name,
                ConfigurationTrustDecisionKind.Abstain,
                Reason: $"{Name} is not supported on Windows."
            ));
        }
        UnixFileMode mode = File.GetUnixFileMode(subject.CanonicalPath);
        if (!RequiredBits.IsDefaultOrEmpty)
        {
            UnixFileMode requiredBits = RequiredBits.Aggregate((a, b) => a | b);
            bool hasRequiredBits = (mode & requiredBits) == requiredBits;
            if (!hasRequiredBits)
            {
                UnixFileMode missingBits = requiredBits & ~mode;
                return ValueTaskOf(new ConfigurationTrustPolicyDecision
                (
                    Name,
                    ConfigurationTrustDecisionKind.Reject,
                    Reason: $"File is missing required permission bits: {missingBits}."
                ));
            }
        }
        if (!ForbiddenBits.IsDefaultOrEmpty)
        {
            UnixFileMode forbiddenBits = ForbiddenBits.Aggregate((a, b) => a | b);
            bool hasForbiddenBits = (mode & forbiddenBits) != 0;
            if (hasForbiddenBits)
            {
                UnixFileMode presentForbiddenBits = mode & forbiddenBits;
                return ValueTaskOf(new ConfigurationTrustPolicyDecision
                (
                    Name,
                    ConfigurationTrustDecisionKind.Reject,
                    Reason: $"File has forbidden permission bits: {presentForbiddenBits}."
                ));
            }
        }
        return ValueTaskOf(new ConfigurationTrustPolicyDecision
        (
            Name,
            ConfigurationTrustDecisionKind.Accept,
            null
        ));
    }
}