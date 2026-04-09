using Mono.Unix;
using System.Collections.Immutable;

namespace Cyborg.Core.Services.Security.Trust.Policies;

public sealed record UnixOwnerTrustPolicy(ImmutableArray<string> AllowedUsers, ImmutableArray<string> AllowedGroups) : TrustPolicyBase("cyborg.trust.policy.unix.owner")
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
        if (!UnixFileSystemInfo.TryGetFileSystemEntry(subject.CanonicalPath, out UnixFileSystemInfo? entry) || entry is not { OwnerUser: { } user, OwnerGroup: { } group })
        {
            return ValueTaskOf(new ConfigurationTrustPolicyDecision
            (
                Name,
                ConfigurationTrustDecisionKind.Reject,
                Reason: "Unable to retrieve file owner information."
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
        if (!AllowedUsers.IsDefaultOrEmpty && AllowedUsers.Contains(user.UserName, StringComparer.Ordinal) || !AllowedGroups.IsDefaultOrEmpty && AllowedGroups.Contains(group.GroupName, StringComparer.Ordinal))
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
            Reason: $"Neither the file owner user '{user.UserName}' nor group '{group.GroupName}' is in the allowed lists."
        ));
    }
}