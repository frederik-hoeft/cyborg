using Cyborg.Core.Configuration;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;
using Cyborg.TestModules.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Tests.Runtime;

[TestClass]
public sealed class EnvironmentCompositionTests : CyborgCoreTestBase
{
    [TestMethod]
    public Task Test_Publish_StoresOnlyLeaves_NotIntermediateObjectsAsync() => TestWithDIAsync(services =>
    {
        IEnvironmentLike environment = services.GetRequiredService<IModuleRuntime>().Environment;
        CompositionNestedOptions value = new(new CompositionLeafOptions("host", 22), "prod");

        environment.Publish("item", value, DecompositionStrategy.LeavesOnly, publishNullValues: true);

        Assert.IsNull(environment["item"]);
        Assert.IsNull(environment["item.nested"]);
        Assert.AreEqual("host", environment.GetLeaf<string>("item.nested.value"));
        Assert.AreEqual(22, environment.GetLeaf<int>("item.nested.count"));
        Assert.AreEqual("prod", environment.GetLeaf<string>("item.label"));
    });

    [TestMethod]
    public Task Test_Publish_FullHierarchyStrategy_StillStoresOnlyLeavesAsync() => TestWithDIAsync(services =>
    {
        IEnvironmentLike environment = services.GetRequiredService<IModuleRuntime>().Environment;
        CompositionNestedOptions value = new(new CompositionLeafOptions("host", 22), "prod");

#pragma warning disable CS0618 // Testing obsolete strategy still publishes leaves only
        environment.Publish("item", value, DecompositionStrategy.FullHierarchy, publishNullValues: true);
#pragma warning restore CS0618

        Assert.IsNull(environment["item"]);
        Assert.IsNull(environment["item.nested"]);
        Assert.AreEqual("host", environment.GetLeaf<string>("item.nested.value"));
    });

    [TestMethod]
    public Task Test_Compose_ReconstructsPublishedDecomposableAsync() => TestWithDIAsync(services =>
    {
        IEnvironmentLike environment = services.GetRequiredService<IModuleRuntime>().Environment;
        CompositionNestedOptions original = new(new CompositionLeafOptions("host", 22), "prod");
        environment.Publish("item", original, DecompositionStrategy.LeavesOnly, publishNullValues: true);

        CompositionNestedOptions composed = environment.Compose<CompositionNestedOptions>("item");

        Assert.AreEqual(original.Nested.Value, composed.Nested.Value);
        Assert.AreEqual(original.Nested.Count, composed.Nested.Count);
        Assert.AreEqual(original.Label, composed.Label);
    });

    [TestMethod]
    public Task Test_Compose_ReflectsLeafOverrideAfterPublishAsync() => TestWithDIAsync(services =>
    {
        IEnvironmentLike environment = services.GetRequiredService<IModuleRuntime>().Environment;
        CompositionNestedOptions original = new(new CompositionLeafOptions("host", 22), "prod");
        environment.Publish("item", original, DecompositionStrategy.LeavesOnly, publishNullValues: true);

        // Property leaf override after composed publish — Compose must observe the override.
        environment.SetVariable("item.nested.value", "overridden");
        environment.SetVariable("item.nested.count", 99);

        CompositionNestedOptions composed = environment.Compose<CompositionNestedOptions>("item");

        Assert.AreEqual("overridden", composed.Nested.Value);
        Assert.AreEqual(99, composed.Nested.Count);
        Assert.AreEqual("prod", composed.Label);
    });

    [TestMethod]
    public Task Test_TryCompose_ReturnsFalseWhenNoLeavesPresentAsync() => TestWithDIAsync(services =>
    {
        IEnvironmentLike environment = services.GetRequiredService<IModuleRuntime>().Environment;

        Assert.IsFalse(environment.TryCompose("missing", out CompositionNestedOptions? _));
    });

    [TestMethod]
    public Task Test_Compose_OptionalNested_IsNullWhenAbsentAsync() => TestWithDIAsync(services =>
    {
        IEnvironmentLike environment = services.GetRequiredService<IModuleRuntime>().Environment;
        CompositionOptionalNestedOptions original = new(Nested: null, Name: "only-name");
        environment.Publish("opt", original, DecompositionStrategy.LeavesOnly, publishNullValues: false);

        CompositionOptionalNestedOptions composed = environment.Compose<CompositionOptionalNestedOptions>("opt");

        Assert.IsNull(composed.Nested);
        Assert.AreEqual("only-name", composed.Name);
    });

    [TestMethod]
    public Task Test_Environment_ImplementsHierarchicalKeyValueStoreAsync() => TestWithDIAsync(services =>
    {
        IEnvironmentLike environment = services.GetRequiredService<IModuleRuntime>().Environment;
        environment.SetVariable("key", "value");

        IHierarchicalKeyValueStore store = environment;
        Assert.IsTrue(store.TryGetValue("key", out string? value));
        Assert.AreEqual("value", value);
        Assert.AreEqual("value", store["key"]);
    });
}

file static class EnvironmentTestExtensions
{
    public static T GetLeaf<T>(this IEnvironmentLike environment, string key)
    {
        Assert.IsTrue(environment.TryGetValue(key, out T? value), $"Expected leaf '{key}' to be present.");
        return value;
    }
}
