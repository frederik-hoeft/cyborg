using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.TestModules.Activation;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Tests.Runtime.Transactions;

[TestClass]
public sealed class RuntimeModuleRegistryTransactionTests
{
    [TestMethod]
    public void Fork_SiblingRegistrationsRemainIsolatedAndConflictOnJoin()
    {
        RuntimeModuleRegistryTransactionParticipant participant = new();
        ExecutionTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction first = fork.CreateChild();
        ExecutionTransaction second = fork.CreateChild();
        ModuleContext firstModule = CreateModuleContext("first");
        ModuleContext secondModule = CreateModuleContext("second");

        Assert.IsTrue(first.GetParticipantState(participant).TryAddModule("shared", firstModule));
        Assert.IsTrue(second.GetParticipantState(participant).TryAddModule("shared", secondModule));
        Assert.IsFalse(fork.Continuation.GetParticipantState(participant).TryGetModule("shared", out ModuleContext? _));
        Assert.AreSame(firstModule, GetRequiredModule(first.GetParticipantState(participant), "shared"));
        Assert.AreSame(secondModule, GetRequiredModule(second.GetParticipantState(participant), "shared"));

        fork.Continuation.Complete();
        first.Complete();
        second.Complete();
        Assert.IsFalse(fork.TryJoin(out TransactionConflict? conflict));
        Assert.IsNotNull(conflict);
        Assert.AreEqual("shared", conflict.LogicalKey);
        Assert.IsFalse(root.GetParticipantState(participant).TryGetModule("shared", out ModuleContext? _));
    }

    [TestMethod]
    public void TryJoin_NestedRegistrationAndRemovalRetainParentRelativeProvenance()
    {
        RuntimeModuleRegistryTransactionParticipant participant = new();
        ModuleContext seededModule = CreateModuleContext("seeded");
        ModuleRegistrySeedBuilder seedBuilder = new();
        seedBuilder.Add("seeded", seededModule);
        TransactionRootSeed rootSeed = new TransactionRootSeed().With(participant, seedBuilder.Build());
        ExecutionTransaction root = new TransactionCoordinator([participant]).CreateRoot(rootSeed);

        ExecutionTransactionForkGroup outerFork = root.Fork();
        ExecutionTransaction parent = outerFork.CreateChild();
        outerFork.Continuation.Complete();
        Assert.IsTrue(parent.GetParticipantState(participant).TryRemoveModule("seeded"));

        ExecutionTransactionForkGroup innerFork = parent.Fork();
        ExecutionTransaction child = innerFork.CreateChild();
        innerFork.Continuation.Complete();
        ModuleContext childModule = CreateModuleContext("child");
        Assert.IsTrue(child.GetParticipantState(participant).TryAddModule("child", childModule));
        child.Complete();
        Assert.IsTrue(innerFork.TryJoin(out TransactionConflict? innerConflict), innerConflict?.LogicalKey.ToString());

        parent.Complete();
        Assert.IsTrue(outerFork.TryJoin(out TransactionConflict? outerConflict), outerConflict?.LogicalKey.ToString());
        RuntimeModuleRegistryTransactionState rootState = root.GetParticipantState(participant);
        Assert.IsFalse(rootState.TryGetModule("seeded", out ModuleContext? _));
        Assert.AreSame(childModule, GetRequiredModule(rootState, "child"));
    }

    [TestMethod]
    public void BindExecutionScope_RegistryFacadeUsesCurrentTransactionState()
    {
        RuntimeModuleRegistry moduleRegistry = new();
        ExecutionTransaction transaction = new TransactionCoordinator([moduleRegistry.Participant]).CreateRoot();
        ServiceCollection services = new();
        services.AddScoped<IModuleRegistry, DefaultModuleRegistry>();
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        moduleRegistry.BindExecutionScope(scope.ServiceProvider, transaction);
        IModuleRegistry registry = scope.ServiceProvider.GetRequiredService<IModuleRegistry>();
        ModuleContext module = CreateModuleContext("module");

        Assert.IsTrue(registry.TryAddModule("module", module));
        Assert.AreSame(module, GetRequiredModule(registry, "module"));
        Assert.IsTrue(registry.TryRemoveModule("module"));
        Assert.IsFalse(registry.TryGetModule("module", out ModuleContext? _));
    }

    [TestMethod]
    public void ApplySeed_RegistersImmutableLoadedGraphIntoCurrentTransaction()
    {
        RuntimeModuleRegistry moduleRegistry = new();
        ExecutionTransaction transaction = new TransactionCoordinator([moduleRegistry.Participant]).CreateRoot();
        ModuleContext module = CreateModuleContext("module");
        ModuleRegistrySeedBuilder seedBuilder = new();
        seedBuilder.Add("module", module);

        moduleRegistry.ApplySeed(transaction, seedBuilder.Build());

        RuntimeModuleRegistryTransactionParticipant participant = (RuntimeModuleRegistryTransactionParticipant)moduleRegistry.Participant;
        RuntimeModuleRegistryTransactionState state = transaction.GetParticipantState(participant);
        Assert.AreSame(module, GetRequiredModule(state, "module"));
    }

    private static ModuleContext CreateModuleContext(string name)
    {
        ActivationProbeModule module = new() { Name = name };
        return new ModuleContext(
            new ModuleReference(module, ActivationProbeModule.ModuleId),
            ModuleEnvironment.Default,
            Configuration: null,
            ModuleRequirements.Default);
    }

    private static ModuleContext GetRequiredModule(RuntimeModuleRegistryTransactionState state, string name)
    {
        Assert.IsTrue(state.TryGetModule(name, out ModuleContext? module));
        return module;
    }

    private static ModuleContext GetRequiredModule(IModuleRegistry registry, string name)
    {
        Assert.IsTrue(registry.TryGetModule(name, out ModuleContext? module));
        return module;
    }
}
