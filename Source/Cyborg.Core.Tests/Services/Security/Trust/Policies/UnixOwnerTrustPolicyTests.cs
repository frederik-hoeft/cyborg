using Cyborg.Core.Services.Security.Trust;
using Cyborg.Core.Services.Security.Trust.Policies;
using System.Runtime.InteropServices;

namespace Cyborg.Core.Tests.Services.Security.Trust.Policies;

[TestClass]
public sealed class UnixOwnerTrustPolicyTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task EvaluateAsync_AcceptsFileOwnedByAllowedUserOrGroup_OnLinuxAsync()
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.OSArchitecture != Architecture.X64)
        {
            Assert.Inconclusive("Unix owner trust policy tests require Linux x64.");
        }

        string path = Path.GetTempFileName();
        try
        {
            bool foundOwner = UnixFileOwnershipResolver.TryGetOwnerAndGroup(path, out string? userName, out string? groupName);

            Assert.IsTrue(foundOwner);
            Assert.IsFalse(string.IsNullOrEmpty(userName));
            Assert.IsFalse(string.IsNullOrEmpty(groupName));
            Assert.IsNotNull(userName);
            Assert.IsNotNull(groupName);

            UnixOwnerTrustPolicy policy = new(AllowedUsers: [userName], AllowedGroups: [groupName]);

            ConfigurationTrustPolicyDecision decision = await policy.EvaluateAsync(new NullServiceProvider(), new ConfigurationTrustSubject(path), TestContext.CancellationToken);

            Assert.AreEqual(ConfigurationTrustDecisionKind.Accept, decision.Decision);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task EvaluateAsync_RejectsMissingPath_OnLinuxAsync()
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.OSArchitecture != Architecture.X64)
        {
            Assert.Inconclusive("Unix owner trust policy tests require Linux x64.");
        }

        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}");
        UnixOwnerTrustPolicy policy = new(["root"], ["root"]);

        ConfigurationTrustPolicyDecision decision = await policy.EvaluateAsync(new NullServiceProvider(), new ConfigurationTrustSubject(path), TestContext.CancellationToken);

        Assert.AreEqual(ConfigurationTrustDecisionKind.Reject, decision.Decision);
        Assert.AreEqual("Unable to retrieve file owner information.", decision.Reason);
    }

    [TestMethod]
    public async Task EvaluateAsync_AbstainsWhenAllowedListsAreEmptyAsync()
    {
        UnixOwnerTrustPolicy policy = new(AllowedUsers: [], AllowedGroups: []);

        ConfigurationTrustPolicyDecision decision = await policy.EvaluateAsync(new NullServiceProvider(), new ConfigurationTrustSubject("/tmp/unused"), TestContext.CancellationToken);

        if (OperatingSystem.IsLinux() && RuntimeInformation.OSArchitecture == Architecture.X64)
        {
            Assert.AreEqual(ConfigurationTrustDecisionKind.Abstain, decision.Decision);
            Assert.AreEqual("No allowed users or groups specified.", decision.Reason);
            return;
        }

        Assert.AreEqual(ConfigurationTrustDecisionKind.Abstain, decision.Decision);
        Assert.AreEqual($"{policy.Name} is only supported on Linux x64.", decision.Reason);
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
