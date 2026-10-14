using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cyborg.Core.Tests.Runtime;

[TestClass]
public sealed class ModuleOriginEnvironmentTests
{
    private static ModuleOrigin CreateOrigin(string fileName) => new(Path.Combine(Path.GetTempPath(), "cyborg-provenance-tests", fileName));

    [TestMethod]
    public void ContextualRegistry_ResolvesGenericPathsWithoutChangingEnvironmentEvaluation()
    {
        ContextualVariableRegistry registry = ContextualVariableRegistry.Default.With(
            new ContextualVariableRegistry.Registration("@.custom.namespace", static (_, ns) => ns));
        DefaultRuntimeEnvironmentFactory factory = new(new(JsonNamingPolicy.SnakeCaseLower), taggedStringConversionObserver: null, contextualVariables: registry);
        GlobalRuntimeEnvironment environment = factory.CreateGlobalEnvironment() with { Namespace = "custom" };
        environment.SetVariable("target", "ordinary");
        environment.SetVariable("text", "${@.custom.namespace}/${@@.custom.namespace}/${@target}");
        environment.SetVariable("lazy", "&{@.custom.namespace}");
        environment.SetVariable("captured", "*{@.custom.namespace}");

        Assert.IsTrue(environment.TryResolveVariable("text", out string? text));
        Assert.AreEqual("custom/custom/ordinary", text);
        Assert.IsTrue(environment.TryResolveVariable("lazy", out string? lazy));
        Assert.AreEqual("custom", lazy);
        Assert.IsTrue(environment.TryResolveVariable("captured", out string? captured));
        Assert.AreEqual("custom", captured);
        Assert.ThrowsExactly<ArgumentException>(() => environment.SetVariable("@.custom.namespace", "spoofed"));
    }

    [TestMethod]
    public void UnknownContextualPaths_AreUndefinedAndCannotBeWritten()
    {
        ModuleOrigin origin = CreateOrigin("parent.jconf");
        GlobalRuntimeEnvironment parent = new(JsonNamingPolicy.SnakeCaseLower) { Context = new EnvironmentContext(origin) };
        InheritedRuntimeEnvironment child = new("child", parent, IsTransient: true, parent.SyntaxFactory, "child");
        parent.SetVariable("template", "${@.invalid}|${@@.invalid}|${@.origin.unknown}");
        parent.SetVariable("lazy", "&{@.invalid}");

        Assert.IsFalse(parent.TryResolveVariable("@.invalid", out string? _));
        Assert.IsFalse(child.TryResolveVariable("@.invalid", out string? _));
        Assert.IsFalse(child.TryResolveVariable("@.origin.unknown", out string? _));
        Assert.IsTrue(child.TryResolveVariable("template", out string? text));
        Assert.AreEqual("${@.invalid}|${@@.invalid}|${@.origin.unknown}", text);
        Assert.ThrowsExactly<InvalidOperationException>(() => child.TryResolveVariable("lazy", out string? _));
        Assert.ThrowsExactly<InvalidOperationException>(() => child.SetVariable("snapshot", "*{@@.invalid}"));
        Assert.ThrowsExactly<ArgumentException>(() => child.SetVariable("@.invalid", "spoofed"));
        Assert.ThrowsExactly<ArgumentException>(() => child.TryRemoveVariable("@.invalid"));
    }

    [TestMethod]
    public void ContextualExpressions_UseCurrentAndEntryPointBindings()
    {
        ModuleOrigin definingOrigin = CreateOrigin("definition.jconf");
        ModuleOrigin callerOrigin = CreateOrigin("caller.jconf");
        GlobalRuntimeEnvironment global = new(JsonNamingPolicy.SnakeCaseLower)
        {
            Namespace = "defining",
            Context = new EnvironmentContext(definingOrigin)
        };
        InheritedRuntimeEnvironment child = new("child", global, IsTransient: true, global.SyntaxFactory, "caller")
        {
            Context = new EnvironmentContext(callerOrigin)
        };
        global.SetVariable("path", "${@.origin.file}|${@@.origin.file}|${@}|${@@}");
        global.SetVariable("lazy", "&{@.origin.directory}");
        global.SetVariable("late", "&{@@.origin.directory}");

        Assert.IsTrue(child.TryResolveVariable("path", out string? path));
        Assert.AreEqual($"{definingOrigin.File}|{callerOrigin.File}|defining|caller", path);
        Assert.IsTrue(child.TryResolveVariable("lazy", out string? lazy));
        Assert.AreEqual(definingOrigin.Directory, lazy);
        Assert.IsTrue(child.TryResolveVariable("late", out string? late));
        Assert.AreEqual(callerOrigin.Directory, late);
    }

    [TestMethod]
    public void ContextualExpressions_MissingOriginDoesNotFallBackToStructuralParent()
    {
        ModuleOrigin origin = CreateOrigin("parent.jconf");
        GlobalRuntimeEnvironment parent = new(JsonNamingPolicy.SnakeCaseLower) { Context = new EnvironmentContext(origin) };
        InheritedRuntimeEnvironment child = new("child", parent, IsTransient: true, parent.SyntaxFactory, "child");
        parent.SetVariable("lazy", "&{@@.origin.file}");

        Assert.IsFalse(child.TryResolveVariable("@.origin.file", out string? _));
        Assert.AreEqual("${@.origin.file}", child.Interpolate("${@.origin.file}").Value);
        Assert.ThrowsExactly<InvalidOperationException>(() => child.SetVariable("captured", "*{@.origin.file}"));
        Assert.ThrowsExactly<InvalidOperationException>(() => child.TryResolveVariable("lazy", out string? _));
        Assert.ThrowsExactly<ArgumentException>(() => child.SetVariable("@.origin.file", "spoofed"));
        Assert.ThrowsExactly<ArgumentException>(() => child.TryRemoveVariable("@.origin.file"));
    }

    [TestMethod]
    public void ContextualCapture_IsTerminalAndIndependentOfSubsequentBindings()
    {
        ModuleOrigin first = CreateOrigin("first.jconf");
        ModuleOrigin second = CreateOrigin("second.jconf");
        GlobalRuntimeEnvironment original = new(JsonNamingPolicy.SnakeCaseLower) { Context = new EnvironmentContext(first) };
        original.SetVariable("snapshot", "*{@.origin.file}");
        original.SetVariable("entry_snapshot", "*{@@.origin.directory}");
        original.SetVariable("dynamic", "&{@.origin.file}");
        GlobalRuntimeEnvironment rebound = original with { Context = new EnvironmentContext(second) };

        Assert.IsTrue(rebound.TryResolveVariable("snapshot", out string? snapshot));
        Assert.AreEqual(first.File, snapshot);
        Assert.IsTrue(rebound.TryResolveVariable("entry_snapshot", out string? entrySnapshot));
        Assert.AreEqual(first.Directory, entrySnapshot);
        Assert.IsTrue(rebound.TryResolveVariable("dynamic", out string? dynamicValue));
        Assert.AreEqual(second.File, dynamicValue);
    }

    [TestMethod]
    public void NamedInheritedEnvironment_PreservesParentOriginAcrossRegistryMaterialization()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        GlobalRuntimeEnvironment global = new(JsonNamingPolicy.SnakeCaseLower);
        ModuleOrigin creatorOrigin = CreateOrigin("creator.jconf");
        ModuleOrigin readerOrigin = CreateOrigin("reader.jconf");
        IRuntimeEnvironment creatorView = global.Bind("creator").WithContext(new EnvironmentContext(creatorOrigin));
        // Capturing the view must not make its definition context depend on later runtime frames.
        RuntimeEnvironmentContext root = CreateRuntimeContext(global, loggerFactory);
        RuntimeEnvironmentContext creator = root.CreateChildFrame(creatorView);
        creator.Environment.SetVariable("who", "${@.origin.file}/${@@.origin.file}");
        IRuntimeEnvironment created = creator.PrepareEnvironment(new ModuleEnvironment { Scope = EnvironmentScope.InheritParent, Name = "session" });
        IRuntimeEnvironment reloaded = root.PrepareEnvironment(new ModuleEnvironment { Scope = EnvironmentScope.Reference, Name = "session" });
        IRuntimeEnvironment callerView = reloaded.Bind("reader").WithContext(new EnvironmentContext(readerOrigin));

        InheritedRuntimeEnvironment initial = Assert.IsInstanceOfType<InheritedRuntimeEnvironment>(created);
        InheritedRuntimeEnvironment reconstructed = Assert.IsInstanceOfType<InheritedRuntimeEnvironment>(reloaded);
        Assert.AreEqual(creatorOrigin, ((RuntimeEnvironment)initial.Parent).Context.Origin);
        Assert.AreEqual(creatorOrigin, ((RuntimeEnvironment)reconstructed.Parent).Context.Origin);
        Assert.IsTrue(callerView.TryResolveVariable("who", out string? result));
        Assert.AreEqual($"{creatorOrigin.File}/{readerOrigin.File}", result);
    }

    [TestMethod]
    public void NamedInheritedEnvironment_OriginIsPublishedOnlyWhenTheCreatingTransactionJoins()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        GlobalRuntimeEnvironment global = new(JsonNamingPolicy.SnakeCaseLower);
        RuntimeEnvironmentTransactionParticipant participant = new();
        RuntimeEnvironmentNode globalNode = new(global.Name, global.IsTransient, Parent: null);
        RuntimeEnvironmentSeed environmentSeed = new(global.EnvironmentId, globalNode, [.. global], RegisterName: false);
        RuntimeEnvironmentTransactionSeed seed = new(global.EnvironmentId, [environmentSeed]);
        ModuleTransaction transaction = new TransactionCoordinator([participant]).CreateRoot(new TransactionRootSeed().With(participant, seed));
        DefaultRuntimeEnvironmentFactory factory = new(global.SyntaxFactory, taggedStringConversionObserver: null);
        RuntimeEnvironmentContext root = RuntimeEnvironmentContext.CreateRoot(global, factory, participant, new ActiveTransaction(transaction), loggerFactory);
        ModuleOrigin origin = CreateOrigin("created-in-child.jconf");

        ModuleTransactionForkGroup discardedFork = transaction.Fork();
        ModuleTransaction abandoned = discardedFork.CreateChild();
        RuntimeEnvironmentContext abandonedContext = root.RebindTransaction(new ActiveTransaction(abandoned));
        RuntimeEnvironmentContext abandonedCreator = abandonedContext.CreateChildFrame(abandonedContext.Environment.WithContext(new EnvironmentContext(origin)));
        abandonedCreator.PrepareEnvironment(new ModuleEnvironment { Scope = EnvironmentScope.InheritParent, Name = "discarded" });
        discardedFork.Discard();
        Assert.ThrowsExactly<InvalidOperationException>(() => root.PrepareEnvironment(new ModuleEnvironment { Scope = EnvironmentScope.Reference, Name = "discarded" }));

        ModuleTransactionForkGroup committedFork = transaction.Fork();
        ModuleTransaction child = committedFork.CreateChild();
        RuntimeEnvironmentContext childContext = root.RebindTransaction(new ActiveTransaction(child));
        RuntimeEnvironmentContext creator = childContext.CreateChildFrame(childContext.Environment.WithContext(new EnvironmentContext(origin)));
        creator.PrepareEnvironment(new ModuleEnvironment { Scope = EnvironmentScope.InheritParent, Name = "published" });
        committedFork.Continuation.Complete();
        child.Complete();
        Assert.IsTrue(committedFork.TryJoin(out TransactionConflict? conflict));
        Assert.IsNull(conflict);

        InheritedRuntimeEnvironment restored = Assert.IsInstanceOfType<InheritedRuntimeEnvironment>(root.PrepareEnvironment(
            new ModuleEnvironment { Scope = EnvironmentScope.Reference, Name = "published" }));
        Assert.AreEqual(origin, ((RuntimeEnvironment)restored.Parent).Context.Origin);
        Assert.IsFalse(root.Environment.TryResolveVariable("@.origin.file", out string? _));
    }

    private static RuntimeEnvironmentContext CreateRuntimeContext(GlobalRuntimeEnvironment environment, ILoggerFactory loggerFactory)
    {
        // Use the production runtime's transaction seed and factory to exercise registry reconstruction.
        RuntimeEnvironmentTransactionParticipant participant = new();
        RuntimeEnvironmentNode globalNode = new(environment.Name, environment.IsTransient, Parent: null);
        RuntimeEnvironmentSeed environmentSeed = new(environment.EnvironmentId, globalNode, [.. environment], RegisterName: false);
        RuntimeEnvironmentTransactionSeed seed = new(environment.EnvironmentId, [environmentSeed]);
        ModuleTransaction transaction = new TransactionCoordinator([participant]).CreateRoot(new TransactionRootSeed().With(participant, seed));
        DefaultRuntimeEnvironmentFactory factory = new(environment.SyntaxFactory, taggedStringConversionObserver: null);
        return RuntimeEnvironmentContext.CreateRoot(environment, factory, participant, new ActiveTransaction(transaction), loggerFactory);
    }
}
