using Cyborg.Core.Configuration;
using Cyborg.Core.Configuration.Builders;
using Cyborg.Core.Configuration.Model;
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
        },
        buildConfiguration: configuration => configuration.AddDictionary(new Dictionary<string, object>
        {
            ["test"] = new TestOptions(new TestNestedOptions("value")),
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
                ["test"] = new TestOptions(new TestNestedOptions("initial")),
            });
            configuration.AddDictionary(new Dictionary<string, object>
            {
                ["test.nested.value"] = "override",
            });
        });

    private sealed record TestOptions(TestNestedOptions Nested) : IDecomposable
    {
        public IEnumerable<DynamicKeyValuePair> Decompose() => [new("nested", Nested)];
    }

    private sealed record TestNestedOptions(string Value) : IDecomposable
    {
        public IEnumerable<DynamicKeyValuePair> Decompose() => [new("value", Value)];
    }
}
