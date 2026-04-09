using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Cyborg.Core.Services.Security.Trust.Policies;

public sealed record UnixOwnerTrustPolicy(ImmutableArray<string> AllowedUsers, ImmutableArray<string> AllowedGroups) : TrustPolicyBase("cyborg.trust.policy.unix.owner")
{
    public override ValueTask<ConfigurationTrustPolicyDecision> EvaluateAsync(IServiceProvider serviceProvider, ConfigurationTrustSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (!OperatingSystem.IsLinux() || RuntimeInformation.OSArchitecture != Architecture.X64)
        {
            return ValueTaskOf(new ConfigurationTrustPolicyDecision
            (
                Name,
                ConfigurationTrustDecisionKind.Abstain,
                Reason: $"{Name} is only supported on Linux x64."
            ));
        }
        if (AllowedUsers.IsDefaultOrEmpty && AllowedGroups.IsDefaultOrEmpty)
        {
            return ValueTaskOf(new ConfigurationTrustPolicyDecision
            (
                Name,
                ConfigurationTrustDecisionKind.Abstain,
                Reason: "No allowed users or groups specified."
            ));
        }
        if (!UnixFileOwnershipResolver.TryGetOwnerAndGroup(subject.CanonicalPath, out string? userName, out string? groupName))
        {
            return ValueTaskOf(new ConfigurationTrustPolicyDecision
            (
                Name,
                ConfigurationTrustDecisionKind.Reject,
                Reason: "Unable to retrieve file owner information."
            ));
        }
        bool isAllowedUser = !AllowedUsers.IsDefaultOrEmpty && AllowedUsers.Contains(userName, StringComparer.Ordinal);
        bool isAllowedGroup = !AllowedGroups.IsDefaultOrEmpty && AllowedGroups.Contains(groupName, StringComparer.Ordinal);
        if (isAllowedUser || isAllowedGroup)
        {
            return ValueTaskOf(new ConfigurationTrustPolicyDecision
            (
                Name,
                ConfigurationTrustDecisionKind.Accept,
                null
            ));
        }
        return ValueTaskOf(new ConfigurationTrustPolicyDecision
        (
            Name,
            ConfigurationTrustDecisionKind.Reject,
            Reason: $"Neither the file owner user '{userName}' nor group '{groupName}' is in the allowed lists."
        ));
    }
}
