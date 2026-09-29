using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Runtime.Services.Transactions;

namespace Cyborg.Core.Tests.Runtime.Transactions;

[TestClass]
public sealed class TransactionCompletionPolicyTests
{
    [TestMethod]
    public void Resolve_UnconfiguredModuleUsesGlobalDefault()
    {
        DefaultTransactionCompletionPolicy policy = new(new TestOptionsProvider(TransactionOnError.Rollback));
        ProbeModule module = new();

        Assert.AreEqual(TransactionPublicationDisposition.Rollback, policy.Resolve(module, ModuleExitStatus.Failed));
        Assert.AreEqual(TransactionPublicationDisposition.Rollback, policy.Resolve(module, ModuleExitStatus.Canceled));
        Assert.AreEqual(TransactionPublicationDisposition.Commit, policy.Resolve(module, ModuleExitStatus.Success));
        Assert.AreEqual(TransactionPublicationDisposition.Commit, policy.Resolve(module, ModuleExitStatus.Skipped));
    }

    [TestMethod]
    public void Resolve_ExplicitModuleSettingOverridesGlobalDefault()
    {
        DefaultTransactionCompletionPolicy policy = new(new TestOptionsProvider(TransactionOnError.Rollback));
        ProbeModule module = new() { Transaction = new ModuleTransactionSettings(TransactionOnError.Commit) };

        Assert.AreEqual(TransactionPublicationDisposition.Commit, policy.Resolve(module, ModuleExitStatus.Failed));
    }

    [TestMethod]
    public void Resolve_MissingProviderPreservesLegacyCommitBehavior()
    {
        DefaultTransactionCompletionPolicy policy = new(options: null);

        Assert.AreEqual(TransactionPublicationDisposition.Commit, policy.Resolve(new ProbeModule(), ModuleExitStatus.Failed));
    }

    private sealed record ProbeModule : ModuleBase;

    private sealed class TestOptionsProvider(TransactionOnError onError) : ITransactionOptionsProvider
    {
        public TransactionOnError OnError => onError;
    }
}