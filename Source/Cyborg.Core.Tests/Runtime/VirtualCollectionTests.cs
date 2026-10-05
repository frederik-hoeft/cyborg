using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Environments.Artifacts;
using Cyborg.Core.Runtime.Engine.Environments.VirtualCollections;
using Cyborg.Core.Runtime.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Tests.Runtime;

[TestClass]
public sealed class VirtualCollectionTests : CyborgCoreTestBase
{
    [TestMethod]
    public Task Append_CreatesCollectionAndPreservesElementIdentityAndOrderAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().GlobalEnvironment;
        SampleHost host = new("alpha");

        environment.SetVariable("items[]+", "first");
        environment.SetVariable("items[]+", 2);
        environment.SetVariable("items[]+", host);
        environment.SetVariable<object?>("items[]+", null);

        object?[] items = ReadSnapshot(environment, "items[]");

        Assert.HasCount(4, items);
        Assert.AreEqual("first", items[0]);
        Assert.AreEqual(2, items[1]);
        Assert.AreSame(host, items[2]);
        Assert.IsNull(items[3]);
        Assert.IsFalse(environment.TryResolveVariable("items", out object? _));
        Assert.IsFalse(environment.TryResolveVariable("items[]+", out object? _));
    });

    [TestMethod]
    public Task Define_DistinguishesEmptyCollectionFromUndefinedVariableAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().GlobalEnvironment;

        Assert.IsFalse(environment.TryResolveVariable("items[]", out IEnumerable<object>? _));
        environment.SetVariable<object?>("items[]", null);

        Assert.IsTrue(environment.TryResolveVariable("items[]", out IEnumerable<object>? defined));
        Assert.IsNotNull(defined);
        Assert.IsEmpty(defined);
        Assert.IsTrue(environment.TryResolveVariable("items[+]", out IEnumerable<object>? live));
        Assert.IsNotNull(live);
        Assert.IsEmpty(live);
    });

    [TestMethod]
    public Task Define_ReplacesElementsAndKeepsStringIntactAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().GlobalEnvironment;
        environment.SetVariable("items[]+", "old");
        environment.SetVariable("letters[]+", "zzz");

        environment.SetVariable("items[]", new object[] { "new", 3 });
        environment.SetVariable("letters[]", "ab");
        environment.SetVariable("wrapped[]+", new List<string> { "not", "expanded" });

        Assert.AreSequenceEqual(new object?[] { "new", 3 }, ReadSnapshot(environment, "items[]"));
        Assert.AreSequenceEqual(new object?[] { "ab" }, ReadSnapshot(environment, "letters[]"));
        object?[] wrapped = ReadSnapshot(environment, "wrapped[]");
        List<string> list = Assert.IsInstanceOfType<List<string>>(wrapped.Single());
        Assert.AreEqual("not", list[0]);
        Assert.AreEqual("expanded", list[1]);
    });

    [TestMethod]
    public Task Snapshot_StaysStableWhileLiveEnumerationObservesAppendsAndStopsAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().GlobalEnvironment;
        environment.SetVariable("items[]+", "a");

        Assert.IsTrue(environment.TryResolveVariable("items[]", out IEnumerable<object>? snapshot));
        Assert.IsNotNull(snapshot);
        using (IEnumerator<object> stable = snapshot.GetEnumerator())
        {
            Assert.IsTrue(stable.MoveNext());
            Assert.AreEqual("a", stable.Current);
            environment.SetVariable("items[]+", "b");
            Assert.IsFalse(stable.MoveNext());
        }

        Assert.IsTrue(environment.TryResolveVariable("items[+]", out IEnumerable<object>? live));
        Assert.IsNotNull(live);
        using (IEnumerator<object> enumerator = live.GetEnumerator())
        {
            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual("a", enumerator.Current);
            environment.SetVariable("items[]+", "c");
            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual("b", enumerator.Current);
            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual("c", enumerator.Current);
            Assert.IsFalse(enumerator.MoveNext());
            environment.SetVariable("items[]+", "d");
            Assert.IsFalse(enumerator.MoveNext());
        }

        Assert.AreSequenceEqual(new object?[] { "a", "b", "c", "d" }, ReadSnapshot(environment, "items[]"));
    });

    [TestMethod]
    public void LiveView_LateVisibleEarlierElement_IsYieldedOnceAfterCurrentBatch()
    {
        MutableEnvironmentVariableStore store = new();
        store.SetValue(VirtualCollectionKeys.Element("items", "00000000000000000002"), "second");
        VirtualCollectionLiveView view = new(store, "items");
        using IEnumerator<object?> enumerator = view.GetEnumerator();

        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual("second", enumerator.Current);

        store.SetValue(VirtualCollectionKeys.Element("items", "00000000000000000001"), "first");
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual("first", enumerator.Current);
        Assert.IsFalse(enumerator.MoveNext());
    }

    [TestMethod]
    public Task Enumeration_HidesStorageAndExposesSnapshotAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().GlobalEnvironment;
        environment.SetVariable("plain", "value");
        environment.SetVariable("items[]+", "element");
        environment.SetVariable<object?>("empty[]", null);

        Dictionary<string, object?> visible = environment.ToDictionary(static pair => pair.Key, static pair => pair.Value);

        Assert.IsTrue(visible.ContainsKey("plain"));
        Assert.AreEqual("value", visible["plain"]);
        Assert.IsTrue(visible.ContainsKey("items[]"));
        Assert.IsTrue(visible.ContainsKey("empty[]"));
        Assert.AreSequenceEqual(new object?[] { "element" }, (object?[])visible["items[]"]!);
        Assert.IsEmpty((object?[])visible["empty[]"]!);
        Assert.IsFalse(visible.Keys.Any(static key => key.Contains('\u001F') || key.EndsWith("[]+", StringComparison.Ordinal) || key.EndsWith("[+]", StringComparison.Ordinal)));
        Assert.ThrowsExactly<ArgumentException>(() => environment.SetVariable(VirtualCollectionKeys.Marker("items"), "hidden"));
        Assert.IsFalse(environment.TryResolveVariable(VirtualCollectionKeys.Element("items", "!0000000000"), out object? _));
        Assert.IsFalse(environment.TryRemoveVariable(VirtualCollectionKeys.Marker("items")));
    });

    [TestMethod]
    public Task Remove_DropsCollectionWithoutRemovingOrdinaryVariableAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().GlobalEnvironment;
        environment.SetVariable("items", "scalar");
        environment.SetVariable("items[]+", "element");

        Assert.IsFalse(environment.TryRemoveVariable("items[]+"));
        Assert.IsTrue(environment.TryRemoveVariable("items[+]"));
        Assert.IsFalse(environment.TryResolveVariable("items[]", out IEnumerable<object>? _));
        Assert.IsTrue(environment.TryResolveVariable("items", out string? scalar));
        Assert.AreEqual("scalar", scalar);
        Assert.IsFalse(environment.TryRemoveVariable("items[]"));

        environment.SetVariable("items[]+", "again");
        Assert.AreSequenceEqual(new object?[] { "again" }, ReadSnapshot(environment, "items[]"));
    });

    [TestMethod]
    public Task Indirection_PreservesCollectionAndLazyAssignmentIsRejectedAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().GlobalEnvironment;
        environment.SetVariable("items[]+", "alpha");
        environment.SetVariable("alias", "${items[]}");
        environment.SetVariable("live_alias", "${items[+]}");

        Assert.IsTrue(environment.TryResolveVariable("alias", out IEnumerable<object>? aliased));
        Assert.IsNotNull(aliased);
        Assert.AreSequenceEqual(new object[] { "alpha" }, aliased);
        Assert.IsTrue(environment.TryResolveVariable("live_alias", out IEnumerable<object>? live));
        Assert.IsNotNull(live);
        Assert.IsInstanceOfType<VirtualCollectionLiveView>(live);
        Assert.ThrowsExactly<InvalidCastException>(() => environment.TryResolveVariable("items[]", out int _));
        Assert.ThrowsExactly<ArgumentException>(() => environment.SetVariable("items[+]", "nope"));
    });

    [TestMethod]
    public Task VariableOperations_RejectMalformedNamesButAcceptOverrideAddressesAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().GlobalEnvironment;

        environment.SetVariable("@probe.value", 7);
        Assert.IsTrue(environment.TryResolveVariable("@probe.value", out int overrideValue));
        Assert.AreEqual(7, overrideValue);

        Assert.ThrowsExactly<ArgumentException>(() => environment.SetVariable("items[0]", "invalid"));
        Assert.ThrowsExactly<ArgumentException>(() => environment.SetVariable("items[]garbage", "invalid"));
        Assert.ThrowsExactly<ArgumentException>(() => environment.SetVariable("@probe.items[]garbage", "invalid"));
        Assert.ThrowsExactly<ArgumentException>(() => environment.TryResolveVariable("items[0]", out object? _));
        Assert.ThrowsExactly<ArgumentException>(() => environment.TryRemoveVariable("items[0]"));
    });

    [TestMethod]
    public Task ResolveCollection_MaterializesElementTypeAtBindingTimeAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().GlobalEnvironment;
        CollectionProbeModule module = new() { Name = "probe" };
        environment.SetVariable("values[]+", "one");
        environment.SetVariable("values[]+", "two");
        environment.SetVariable("@probe.items", "${values[]}");

        IReadOnlyCollection<string>? resolved = environment.ResolveCollection(module, (IReadOnlyCollection<string>?)null, "module", "module.Items");

        Assert.IsNotNull(resolved);
        Assert.AreEqual(2, resolved.Count);
        Assert.AreEqual("one", resolved.ElementAt(0));
        Assert.AreEqual("two", resolved.ElementAt(1));
        environment.SetVariable("values[]+", 3);
        Assert.ThrowsExactly<InvalidCastException>(() => environment.ResolveCollection(module, (IReadOnlyCollection<string>?)null, "module", "module.Items"));
    });

    [TestMethod]
    public Task ResolveCollection_DirectVirtualOverrideSnapshotsAtBindingTimeAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().GlobalEnvironment;
        CollectionProbeModule module = new() { Name = "probe" };
        environment.SetVariable("@probe.items[]+", "one");
        environment.SetVariable("@probe.items[]+", "two");
        Assert.ThrowsExactly<ArgumentException>(() => environment.SetVariable("@probe.items[+]", "invalid"));

        Assert.IsTrue(environment.TryResolveVariable("@probe.items[]", out IEnumerable<object>? snapshot));
        Assert.IsNotNull(snapshot);
        Assert.AreSequenceEqual(new object[] { "one", "two" }, snapshot);
        Assert.IsTrue(environment.TryResolveVariable("@probe.items[+]", out IEnumerable<object>? live));
        Assert.IsNotNull(live);
        Assert.IsInstanceOfType<VirtualCollectionLiveView>(live);

        IReadOnlyCollection<string>? resolved = environment.ResolveCollection(module, (IReadOnlyCollection<string>?)null, "module", "module.Items");
        Assert.IsNotNull(resolved);
        Assert.AreEqual(2, resolved.Count);
        Assert.AreEqual("one", resolved.ElementAt(0));
        Assert.AreEqual("two", resolved.ElementAt(1));

        environment.SetVariable("@probe.items[]+", "three");
        Assert.AreEqual(2, resolved.Count);
        IReadOnlyCollection<string>? rebound = environment.ResolveCollection(module, (IReadOnlyCollection<string>?)null, "module", "module.Items");
        Assert.IsNotNull(rebound);
        Assert.AreEqual(3, rebound.Count);
        Assert.AreEqual("one", rebound.ElementAt(0));
        Assert.AreEqual("two", rebound.ElementAt(1));
        Assert.AreEqual("three", rebound.ElementAt(2));
    });

    [TestMethod]
    public Task ResolveCollection_OrdinaryOverrideTakesPrecedenceOverVirtualOverrideAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().GlobalEnvironment;
        CollectionProbeModule module = new() { Name = "probe" };
        environment.SetVariable("@probe.items[]+", "virtual");
        string[] ordinaryOverride = ["ordinary"];
        environment.SetVariable("@probe.items", ordinaryOverride);

        IReadOnlyCollection<string>? resolved = environment.ResolveCollection(module, (IReadOnlyCollection<string>?)null, "module", "module.Items");

        Assert.IsNotNull(resolved);
        Assert.AreEqual(1, resolved.Count);
        Assert.AreEqual("ordinary", resolved.Single());
    });

    [TestMethod]
    [DataRow(DecompositionStrategy.LeavesOnly)]
    [DataRow(DecompositionStrategy.Shallow)]
    [DataRow(DecompositionStrategy.FullHierarchy)]
    public Task Publish_CollectionAppendRoot_StoresElementWithoutLeavesAsync(DecompositionStrategy strategy) => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().GlobalEnvironment;
        SampleHost host = new("alpha");

        environment.Publish("hosts[]+", host, strategy, publishNullValues: true);

        object?[] hosts = ReadSnapshot(environment, "hosts[]");
        SampleHost published = Assert.IsInstanceOfType<SampleHost>(hosts.Single());
        Assert.AreSame(host, published);
        Assert.IsFalse(environment.TryResolveVariable("hosts.hostname", out string? _));
        Assert.ThrowsExactly<ArgumentException>(() => environment.TryResolveVariable("hosts[]+.hostname", out string? _));
        Assert.IsFalse(environment.Any(static pair => pair.Key.Contains("hostname", StringComparison.Ordinal)));
    });

    [TestMethod]
    public Task Publish_ReplaysVisibleCollectionIntoTargetAsync() => TestWithDIAsync(services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IEnvironmentLike artifacts = new EnvironmentLike(runtime.Environment.SyntaxFactory, "artifacts");
        artifacts.SetVariable("plain", 7);
        artifacts.SetVariable("items[]+", "one");
        artifacts.SetVariable<object?>("empty[]", null);
        artifacts.SetVariable("items[]+", "two");

        runtime.Environment.Publish(artifacts);

        Assert.IsTrue(runtime.Environment.TryResolveVariable("plain", out int plain));
        Assert.AreEqual(7, plain);
        Assert.AreSequenceEqual(new object?[] { "one", "two" }, ReadSnapshot(runtime.Environment, "items[]"));
        Assert.IsTrue(runtime.Environment.TryResolveVariable("empty[]", out IEnumerable<object>? empty));
        Assert.IsNotNull(empty);
        Assert.IsEmpty(empty);
    });

    [TestMethod]
    public Task Scoping_ShadowsInheritedCollectionAndSharesCurrentEnvironmentAsync() => TestWithDIAsync(services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.GlobalEnvironment.SetVariable("items[]+", "parent");
        IRuntimeEnvironment inherited = runtime.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.InheritParent,
            Name = "child"
        });

        Assert.AreSequenceEqual(new object?[] { "parent" }, ReadSnapshot(inherited, "items[]"));
        inherited.SetVariable("items[]+", "local");
        Assert.AreSequenceEqual(new object?[] { "local" }, ReadSnapshot(inherited, "items[]"));
        Assert.AreSequenceEqual(new object?[] { "parent" }, ReadSnapshot(runtime.GlobalEnvironment, "items[]"));

        inherited.SetVariable<object?>("items[]", null);
        Assert.IsTrue(inherited.TryResolveVariable("items[]", out IEnumerable<object>? hidden));
        Assert.IsNotNull(hidden);
        Assert.IsEmpty(hidden);
        Assert.AreSequenceEqual(new object?[] { "parent" }, ReadSnapshot(runtime.GlobalEnvironment, "items[]"));

        IRuntimeEnvironment current = runtime.PrepareEnvironment(new ModuleEnvironment { Scope = EnvironmentScope.Current });
        current.SetVariable("items[]+", "shared");
        Assert.AreSequenceEqual(new object?[] { "parent", "shared" }, ReadSnapshot(runtime.GlobalEnvironment, "items[]"));

        IRuntimeEnvironment isolated = runtime.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.Isolated,
            Name = "box"
        });
        isolated.SetVariable("items[]+", "hidden-element");
        Assert.AreSequenceEqual(new object?[] { "hidden-element" }, ReadSnapshot(isolated, "items[]"));
        Assert.AreSequenceEqual(new object?[] { "parent", "shared" }, ReadSnapshot(runtime.GlobalEnvironment, "items[]"));
    });

    [TestMethod]
    public Task ClrCollection_RemainsOrdinaryVariableAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().GlobalEnvironment;
        object[] values = ["first", "second"];
        environment.SetVariable("items", values);

        Assert.IsTrue(environment.TryResolveVariable("items", out IEnumerable<object>? resolved));
        Assert.AreSame(values, resolved);
        Assert.IsFalse(environment.TryResolveVariable("items[]", out IEnumerable<object>? _));
        Assert.IsTrue(environment.Any(static pair => pair.Key == "items"));
        Assert.IsFalse(environment.Any(static pair => pair.Key == "items[]"));
    });

    private static object?[] ReadSnapshot(IEnvironmentLike environment, string name)
    {
        Assert.IsTrue(environment.TryResolveVariable(name, out IEnumerable<object>? collection));
        Assert.IsNotNull(collection);
        return [.. collection];
    }

    private sealed record CollectionProbeModule : ModuleBase, IModuleDefinition
    {
        public static string ModuleId => "cyborg.tests.virtual-collection.v1";
    }

    private sealed class SampleHost(string hostname) : IDecomposable
    {
        public string Hostname { get; } = hostname;

        public IEnumerable<DynamicKeyValuePair> Decompose() => [new("hostname", Hostname)];
    }
}
