using Cyborg.Core.Configuration;
using Cyborg.Core.Configuration.Builders;
using Cyborg.TestModules.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Tests.Configuration;

[TestClass]
public sealed class DefaultConfigurationTests : CyborgCoreTestBase
{
    [TestMethod]
    public Task Test_DecomposableValue_StoresOnlyLeavesAsync() => TestWithDIAsync(
        assertion: services =>
        {
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();

            Assert.IsNull(configuration["test"]);
            Assert.IsNull(configuration["test.nested"]);
            Assert.AreEqual("value", configuration.Get<string>("test.nested.value"));
            Assert.AreEqual(1, configuration.Get<int>("test.nested.count"));
        },
        buildConfiguration: configuration => configuration.AddDictionary(new Dictionary<string, object>
        {
            ["test"] = new CompositionNestedOptions(new CompositionLeafOptions("value", 1), Label: null),
        }));

    [TestMethod]
    public Task Test_LaterLeafValue_OverridesEarlierStructuredSourceAsync() => TestWithDIAsync(
        assertion: services =>
        {
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();

            Assert.IsNull(configuration["test"]);
            Assert.IsNull(configuration["test.nested"]);
            Assert.AreEqual("override", configuration.Get<string>("test.nested.value"));
        },
        buildConfiguration: configuration =>
        {
            configuration.AddDictionary(new Dictionary<string, object>
            {
                ["test"] = new CompositionNestedOptions(new CompositionLeafOptions("initial", 1), Label: null),
            });
            configuration.AddDictionary(new Dictionary<string, object>
            {
                ["test.nested.value"] = "override",
            });
        });

    [TestMethod]
    public Task Test_Compose_ReconstructsStructuredValueFromLeavesAsync() => TestWithDIAsync(
        assertion: services =>
        {
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();

            CompositionNestedOptions composed = configuration.Compose<CompositionNestedOptions>("test");
            Assert.AreEqual("value", composed.Nested.Value);
            Assert.AreEqual(7, composed.Nested.Count);
            Assert.AreEqual("label", composed.Label);
        },
        buildConfiguration: configuration => configuration.AddDictionary(new Dictionary<string, object>
        {
            ["test"] = new CompositionNestedOptions(new CompositionLeafOptions("value", 7), "label"),
        }));

    [TestMethod]
    public Task Test_Compose_ReflectsLeafOverridesAfterStructuredSourceAsync() => TestWithDIAsync(
        assertion: services =>
        {
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();

            CompositionNestedOptions composed = configuration.Compose<CompositionNestedOptions>("test");
            Assert.AreEqual("override", composed.Nested.Value);
            Assert.AreEqual(1, composed.Nested.Count);
        },
        buildConfiguration: configuration =>
        {
            configuration.AddDictionary(new Dictionary<string, object>
            {
                ["test"] = new CompositionNestedOptions(new CompositionLeafOptions("initial", 1), Label: null),
            });
            configuration.AddDictionary(new Dictionary<string, object>
            {
                ["test.nested.value"] = "override",
            });
        });

    [TestMethod]
    public Task Test_Configuration_ImplementsHierarchicalKeyValueStoreAsync() => TestWithDIAsync(
        assertion: services =>
        {
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();
            IHierarchicalKeyValueStore store = configuration;

            Assert.IsTrue(store.TryGetValue("test.nested.value", out string? value));
            Assert.AreEqual("value", value);
            Assert.IsTrue(store.HasValues("test"));
            Assert.IsFalse(store.HasValues("missing"));
        },
        buildConfiguration: configuration => configuration.AddDictionary(new Dictionary<string, object>
        {
            ["test"] = new CompositionNestedOptions(new CompositionLeafOptions("value", 1), Label: null),
        }));
}
