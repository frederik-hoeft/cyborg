using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cyborg.Core.Tests.Runtime;

/// <summary>
/// Characterizes the boundaries between logical environment identity, structural parent views,
/// runtime invocation ancestry, and transaction-visible environment state before refactoring them.
/// </summary>
[TestClass]
public sealed class RuntimeEnvironmentContextContractTests
{
    [TestMethod]
    public void InheritedNamedEnvironment_ReconstructionPreservesRecordedParentView()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        (RuntimeEnvironmentContext root, _, _) = CreateRoot(loggerFactory);
        root.Environment.SetVariable("template", "${@}/${@@}");
        IRuntimeEnvironment creatorView = root.Environment.Bind("creator").WithOverrideResolutionTags(["creator_tag"]);
        RuntimeEnvironmentContext creator = root.CreateChildFrame(creatorView);
        IRuntimeEnvironment created = creator.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.InheritParent,
            Name = "session"
        });

        IRuntimeEnvironment reconstructed = root.PrepareEnvironment(Reference("session"));
        InheritedRuntimeEnvironment initial = Assert.IsInstanceOfType<InheritedRuntimeEnvironment>(created);
        InheritedRuntimeEnvironment restored = Assert.IsInstanceOfType<InheritedRuntimeEnvironment>(reconstructed);

        Assert.AreEqual(((ITransactionalRuntimeEnvironment)initial).EnvironmentId, ((ITransactionalRuntimeEnvironment)restored).EnvironmentId);
        Assert.AreEqual("creator", initial.Parent.Namespace);
        Assert.AreEqual("creator", restored.Parent.Namespace);
        Assert.IsTrue(initial.Parent.OverrideResolutionTags.SequenceEqual(["creator_tag"]));
        Assert.IsTrue(restored.Parent.OverrideResolutionTags.SequenceEqual(["creator_tag"]));

        IRuntimeEnvironment reader = restored.Bind("reader");
        Assert.IsTrue(reader.TryResolveVariable("template", out string? evaluated));
        Assert.AreEqual("creator/reader", evaluated);
    }

    [TestMethod]
    public void NamedReference_PrefersActiveInvocationViewBeforeTransactionalRegistry()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        (RuntimeEnvironmentContext root, _, _) = CreateRoot(loggerFactory);
        IRuntimeEnvironment created = root.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.Isolated,
            Name = "shared"
        });
        RuntimeEnvironmentContext module = root.CreateChildFrame(created.Bind("module").WithOverrideResolutionTags(["module_tag"]));

        IRuntimeEnvironment activeReference = module.PrepareEnvironment(Reference("shared"));
        IRuntimeEnvironment registryReference = root.PrepareEnvironment(Reference("shared"));

        Assert.AreEqual("module", activeReference.Namespace);
        Assert.IsTrue(activeReference.OverrideResolutionTags.SequenceEqual(["module_tag"]));
        Assert.AreEqual("__UNBOUND", registryReference.Namespace);
        Assert.IsEmpty(registryReference.OverrideResolutionTags);
        Assert.AreEqual(((ITransactionalRuntimeEnvironment)activeReference).EnvironmentId, ((ITransactionalRuntimeEnvironment)registryReference).EnvironmentId);
        activeReference.SetVariable("value", 17);
        Assert.IsTrue(registryReference.TryResolveVariable("value", out int value));
        Assert.AreEqual(17, value);
    }

    [TestMethod]
    public void ScopeSelection_DistinguishesCurrentParentGlobalAndStructuralInheritance()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        (RuntimeEnvironmentContext root, _, _) = CreateRoot(loggerFactory);
        IRuntimeEnvironment frameEnvironment = root.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.Isolated,
            Name = "frame"
        });
        RuntimeEnvironmentContext frame = root.CreateChildFrame(frameEnvironment.Bind("worker"));

        IRuntimeEnvironment current = frame.PrepareEnvironment(new ModuleEnvironment { Scope = EnvironmentScope.Current });
        IRuntimeEnvironment invocationParent = frame.PrepareEnvironment(new ModuleEnvironment { Scope = EnvironmentScope.Parent });
        IRuntimeEnvironment global = frame.PrepareEnvironment(new ModuleEnvironment { Scope = EnvironmentScope.Global });
        IRuntimeEnvironment isolated = frame.PrepareEnvironment(new ModuleEnvironment { Scope = EnvironmentScope.Isolated });
        IRuntimeEnvironment inheritParent = frame.PrepareEnvironment(new ModuleEnvironment { Scope = EnvironmentScope.InheritParent });
        IRuntimeEnvironment inheritGlobal = frame.PrepareEnvironment(new ModuleEnvironment { Scope = EnvironmentScope.InheritGlobal });

        Assert.AreEqual("frame", current.Name);
        Assert.AreEqual("__UNBOUND", current.Namespace);
        Assert.AreEqual(root.Environment.Name, invocationParent.Name);
        Assert.AreEqual("__UNBOUND", invocationParent.Namespace);
        Assert.AreEqual(root.GlobalEnvironment.Name, global.Name);
        Assert.IsNotInstanceOfType<InheritedRuntimeEnvironment>(isolated);
        Assert.IsTrue(isolated.IsTransient);
        InheritedRuntimeEnvironment parentChild = Assert.IsInstanceOfType<InheritedRuntimeEnvironment>(inheritParent);
        InheritedRuntimeEnvironment globalChild = Assert.IsInstanceOfType<InheritedRuntimeEnvironment>(inheritGlobal);
        Assert.AreEqual("frame", parentChild.Parent.Name);
        Assert.AreEqual("worker", parentChild.Parent.Namespace);
        Assert.AreEqual(root.GlobalEnvironment.Name, globalChild.Parent.Name);
        Assert.IsTrue(parentChild.IsTransient);
        Assert.IsTrue(globalChild.IsTransient);
    }

    [TestMethod]
    public void InvocationParent_IsDistinctFromStructuralEnvironmentParent()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        (RuntimeEnvironmentContext root, _, _) = CreateRoot(loggerFactory);
        IRuntimeEnvironment inheritedEnvironment = root.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.InheritGlobal,
            Name = "inherited"
        });
        InheritedRuntimeEnvironment inherited = Assert.IsInstanceOfType<InheritedRuntimeEnvironment>(inheritedEnvironment);
        RuntimeEnvironmentContext outer = root.CreateChildFrame(inherited.Bind("outer").WithOverrideResolutionTags(["outer_tag"]));
        IRuntimeEnvironment independentEnvironment = root.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.Isolated,
            Name = "independent"
        });
        RuntimeEnvironmentContext inner = outer.CreateChildFrame(independentEnvironment.Bind("inner"));

        Assert.AreEqual("inherited", inner.ParentEnvironment.Name);
        Assert.AreEqual("outer", inner.ParentEnvironment.Namespace);
        Assert.IsTrue(inner.ParentEnvironment.OverrideResolutionTags.SequenceEqual(["outer_tag"]));
        Assert.AreEqual(root.GlobalEnvironment.Name, inherited.Parent.Name);
        Assert.AreNotEqual(inner.ParentEnvironment.Namespace, inherited.Parent.Namespace);
        Assert.AreEqual("outer", inner.ResolveEnvironmentReference(new ModuleEnvironmentReference(EnvironmentScopeReference.Parent, Name: null))?.Namespace);

        IRuntimeEnvironment structuralChild = inner.PrepareEnvironment(new ModuleEnvironment { Scope = EnvironmentScope.InheritParent });
        InheritedRuntimeEnvironment structural = Assert.IsInstanceOfType<InheritedRuntimeEnvironment>(structuralChild);
        Assert.AreEqual("independent", structural.Parent.Name);
        Assert.AreEqual("inner", structural.Parent.Namespace);
    }

    [TestMethod]
    public void InvocationViews_RetargetTransactionWithoutChangingAncestryOrContext()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        (RuntimeEnvironmentContext root, ModuleTransaction transaction, _) = CreateRoot(loggerFactory);
        root.Environment.SetVariable("initial", 1);
        ActiveTransaction active = new(transaction);
        RuntimeEnvironmentContext owner = root.RebindTransaction(active);
        RuntimeEnvironmentContext outer = owner.CreateChildFrame(owner.Environment.Bind("outer").WithOverrideResolutionTags(["outer_tag"]));
        RuntimeEnvironmentContext inner = outer.CreateChildFrame(outer.Environment.Bind("inner").WithOverrideResolutionTags(["inner_tag"]));

        ModuleTransactionForkGroup fork = transaction.Fork();
        active.Retarget(fork.Continuation);
        inner.Environment.SetVariable("continuation_only", 2);
        Assert.IsTrue(inner.ParentEnvironment.TryResolveVariable("continuation_only", out int continuationValue));
        Assert.AreEqual(2, continuationValue);

        ModuleTransaction childTransaction = fork.CreateChild();
        RuntimeEnvironmentContext child = inner.RebindTransaction(new ActiveTransaction(childTransaction));
        Assert.AreEqual("inner", child.Environment.Namespace);
        Assert.AreEqual("outer", child.ParentEnvironment.Namespace);
        Assert.IsTrue(child.Environment.OverrideResolutionTags.SequenceEqual(["inner_tag"]));
        Assert.IsTrue(child.ParentEnvironment.OverrideResolutionTags.SequenceEqual(["outer_tag"]));
        Assert.IsTrue(child.Environment.TryResolveVariable("initial", out int initial));
        Assert.AreEqual(1, initial);
        Assert.IsFalse(child.Environment.TryResolveVariable("continuation_only", out int _));
        child.Environment.SetVariable("child_only", 3);
        Assert.IsFalse(inner.Environment.TryResolveVariable("child_only", out int _));

        fork.Continuation.Complete();
        childTransaction.Complete();
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));
        Assert.IsNull(conflict);
        active.Retarget(transaction);
        Assert.AreEqual("inner", inner.Environment.Namespace);
        Assert.AreEqual("outer", inner.ParentEnvironment.Namespace);
        Assert.IsTrue(inner.Environment.OverrideResolutionTags.SequenceEqual(["inner_tag"]));
        Assert.IsTrue(inner.ParentEnvironment.OverrideResolutionTags.SequenceEqual(["outer_tag"]));
        Assert.IsTrue(inner.Environment.TryResolveVariable("child_only", out int committed));
        Assert.AreEqual(3, committed);
    }

    [TestMethod]
    public void NamedEnvironment_ViewTagsDoNotMutateRegisteredViewMetadata()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        (RuntimeEnvironmentContext root, _, _) = CreateRoot(loggerFactory);
        IRuntimeEnvironment tagged = root.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.Isolated,
            Name = "tagged"
        }, overrideResolutionTags: ["custom_tag"]);

        IRuntimeEnvironment plain = root.PrepareEnvironment(Reference("tagged"));

        Assert.IsTrue(tagged.OverrideResolutionTags.SequenceEqual(["custom_tag"]));
        Assert.IsEmpty(plain.OverrideResolutionTags);
        Assert.AreEqual(((ITransactionalRuntimeEnvironment)tagged).EnvironmentId, ((ITransactionalRuntimeEnvironment)plain).EnvironmentId);
    }

    [TestMethod]
    public void TransactionRebinding_PreservesInvocationViewsButIsolatesVariableWritesUntilJoin()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        (RuntimeEnvironmentContext root, ModuleTransaction rootTransaction, _) = CreateRoot(loggerFactory);
        root.Environment.SetVariable("value", "initial");
        RuntimeEnvironmentContext frame = root.CreateChildFrame(root.Environment.Bind("module").WithOverrideResolutionTags(["tag"]));
        ModuleTransactionForkGroup fork = rootTransaction.Fork();
        ModuleTransaction childTransaction = fork.CreateChild();
        RuntimeEnvironmentContext rebound = frame.RebindTransaction(new ActiveTransaction(childTransaction));
        RuntimeEnvironmentContext continuation = frame.RebindTransaction(new ActiveTransaction(fork.Continuation));

        Assert.AreEqual("module", rebound.Environment.Namespace);
        Assert.IsTrue(rebound.Environment.OverrideResolutionTags.SequenceEqual(["tag"]));
        Assert.AreEqual(frame.ParentEnvironment.Namespace, rebound.ParentEnvironment.Namespace);
        rebound.Environment.SetVariable("value", "child");
        // The fork owner is frozen; only the continuation may read the concurrent baseline.
        Assert.IsTrue(continuation.Environment.TryResolveVariable("value", out string? beforeJoin));
        Assert.AreEqual("initial", beforeJoin);

        fork.Continuation.Complete();
        childTransaction.Complete();
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));
        Assert.IsNull(conflict);
        Assert.IsTrue(root.Environment.TryResolveVariable("value", out string? afterJoin));
        Assert.AreEqual("child", afterJoin);
        Assert.AreEqual("module", rebound.Environment.Namespace);
    }

    [TestMethod]
    public void NamedEnvironmentRegistration_IsVisibleOnlyAfterBranchReconciliation()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        (RuntimeEnvironmentContext root, ModuleTransaction rootTransaction, _) = CreateRoot(loggerFactory);
        ModuleTransactionForkGroup fork = rootTransaction.Fork();
        ModuleTransaction childTransaction = fork.CreateChild();
        ModuleTransaction siblingTransaction = fork.CreateChild();
        RuntimeEnvironmentContext child = root.RebindTransaction(new ActiveTransaction(childTransaction));
        RuntimeEnvironmentContext sibling = root.RebindTransaction(new ActiveTransaction(siblingTransaction));
        IRuntimeEnvironment created = child.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.Isolated,
            Name = "from_child"
        });
        created.SetVariable("value", 12);

        Assert.ThrowsExactly<InvalidOperationException>(() => sibling.PrepareEnvironment(Reference("from_child")));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.PrepareEnvironment(Reference("from_child")));
        fork.Continuation.Complete();
        childTransaction.Complete();
        siblingTransaction.Complete();
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));
        Assert.IsNull(conflict);

        IRuntimeEnvironment committed = root.PrepareEnvironment(Reference("from_child"));
        Assert.IsTrue(committed.TryResolveVariable("value", out int value));
        Assert.AreEqual(12, value);
    }

    [TestMethod]
    public void NamedEnvironmentRegistration_DiscardedBranchDoesNotPublishTopologyOrBindings()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        (RuntimeEnvironmentContext root, ModuleTransaction rootTransaction, _) = CreateRoot(loggerFactory);
        ModuleTransactionForkGroup fork = rootTransaction.Fork();
        ModuleTransaction childTransaction = fork.CreateChild();
        RuntimeEnvironmentContext child = root.RebindTransaction(new ActiveTransaction(childTransaction));
        IRuntimeEnvironment created = child.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.Isolated,
            Name = "discarded"
        });
        created.SetVariable("value", 42);

        fork.Discard();

        Assert.ThrowsExactly<InvalidOperationException>(() => root.PrepareEnvironment(Reference("discarded")));
    }

    [TestMethod]
    public void InheritedNamedEnvironment_ReconstructionAfterTransactionForkUsesRecordedParentView()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        (RuntimeEnvironmentContext root, ModuleTransaction rootTransaction, _) = CreateRoot(loggerFactory);
        RuntimeEnvironmentContext creator = root.CreateChildFrame(root.Environment.Bind("creator").WithOverrideResolutionTags(["creator_tag"]));
        creator.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.InheritParent,
            Name = "captured"
        });
        ModuleTransactionForkGroup fork = rootTransaction.Fork();
        ModuleTransaction childTransaction = fork.CreateChild();
        RuntimeEnvironmentContext child = root.RebindTransaction(new ActiveTransaction(childTransaction));

        IRuntimeEnvironment referenced = child.PrepareEnvironment(Reference("captured"));
        InheritedRuntimeEnvironment inherited = Assert.IsInstanceOfType<InheritedRuntimeEnvironment>(referenced);
        Assert.AreEqual("creator", inherited.Parent.Namespace);
        Assert.IsTrue(inherited.Parent.OverrideResolutionTags.SequenceEqual(["creator_tag"]));

        fork.Discard();
    }

    [TestMethod]
    public void ImportedEnvironmentView_PreservesStructuralParentBindingsWithoutRewritingExistingTopology()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        (RuntimeEnvironmentContext source, _, _) = CreateRoot(loggerFactory);
        source.Environment.SetVariable("inherited_value", 23);
        IRuntimeEnvironment definitionView = source.Environment.Bind("definition").WithOverrideResolutionTags(["definition_tag"]);
        RuntimeEnvironmentContext creator = source.CreateChildFrame(definitionView);
        IRuntimeEnvironment created = creator.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.InheritParent,
            Name = "external"
        });
        created.SetVariable("local_value", 42);

        (RuntimeEnvironmentContext target, ModuleTransaction targetTransaction, RuntimeEnvironmentTransactionParticipant targetParticipant) = CreateRoot(loggerFactory);
        IRuntimeEnvironment imported = target.BindToCurrentTransaction(created);
        InheritedRuntimeEnvironment importedChild = Assert.IsInstanceOfType<InheritedRuntimeEnvironment>(imported);
        Assert.AreEqual("definition", importedChild.Parent.Namespace);
        Assert.IsTrue(importedChild.Parent.OverrideResolutionTags.SequenceEqual(["definition_tag"]));
        Assert.IsTrue(imported.TryResolveVariable("inherited_value", out int inherited));
        Assert.AreEqual(23, inherited);
        Assert.IsTrue(imported.TryResolveVariable("local_value", out int local));
        Assert.AreEqual(42, local);

        // A different view of the same logical node must not replace its already imported structural metadata.
        InheritedRuntimeEnvironment differentlyBound = importedChild with { Parent = importedChild.Parent.Bind("other") };
        target.BindToCurrentTransaction(differentlyBound);
        RuntimeEnvironmentTransactionState state = targetTransaction.GetParticipantState(targetParticipant);
        RuntimeEnvironmentId importedId = ((ITransactionalRuntimeEnvironment)imported).EnvironmentId;
        Assert.IsTrue(state.TryGetEnvironment(importedId, out RuntimeEnvironmentNode? node));
        Assert.IsNotNull(node.Parent);
        Assert.AreEqual("definition", node.Parent.Namespace);
        Assert.IsTrue(node.Parent.OverrideResolutionTags.SequenceEqual(["definition_tag"]));
    }

    [TestMethod]
    public void NamedEnvironment_ReferenceFailsWithoutAVisibleRegistration()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        (RuntimeEnvironmentContext root, _, _) = CreateRoot(loggerFactory);

        Assert.ThrowsExactly<InvalidOperationException>(() => root.PrepareEnvironment(Reference("absent")));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.Reference,
            Name = null
        }));
    }

    private static ModuleEnvironment Reference(string name) => new() { Scope = EnvironmentScope.Reference, Name = name };

    private static (RuntimeEnvironmentContext Context, ModuleTransaction Transaction, RuntimeEnvironmentTransactionParticipant Participant) CreateRoot(ILoggerFactory loggerFactory)
    {
        GlobalRuntimeEnvironment global = new(JsonNamingPolicy.SnakeCaseLower);
        RuntimeEnvironmentTransactionParticipant participant = new();
        RuntimeEnvironmentNode globalNode = new(global.Name, global.IsTransient, Parent: null);
        RuntimeEnvironmentSeed environmentSeed = new(global.EnvironmentId, globalNode, [.. global], RegisterName: false);
        RuntimeEnvironmentTransactionSeed seed = new(global.EnvironmentId, [environmentSeed]);
        ModuleTransaction transaction = new TransactionCoordinator([participant]).CreateRoot(new TransactionRootSeed().With(participant, seed));
        DefaultRuntimeEnvironmentFactory factory = new(global.SyntaxFactory, taggedStringConversionObserver: null);
        RuntimeEnvironmentContext context = RuntimeEnvironmentContext.CreateRoot(global, factory, participant, new ActiveTransaction(transaction), loggerFactory);
        return (context, transaction, participant);
    }
}
