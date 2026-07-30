using Cyborg.Core.Modules.Descriptors;
using Cyborg.Core.Modules.Descriptors.Builders;
using System.Text.Json;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class ModuleDescriptionTests
{
    [TestMethod]
    public void ToText_RendersNestedObjectsAndCollections()
    {
        TestDescriptor descriptor = new();

        string result = ModuleDescription.ToText(descriptor);

        Assert.Contains("ModuleId: \"cyborg.tests.description.v1\"", result);
        Assert.Contains("Options:", result);
        Assert.Contains("  Enabled: true", result);
        Assert.Contains("Items:", result);
        Assert.Contains("  [0]: \"first\"", result);
        Assert.Contains("  [1]:", result);
        Assert.Contains("    Value: 42", result);
    }

    [TestMethod]
    public void ToJson_RendersValidNestedJson()
    {
        TestDescriptor descriptor = new();

        string result = ModuleDescription.ToJson(descriptor);
        using JsonDocument document = JsonDocument.Parse(result);

        JsonElement root = document.RootElement;
        Assert.AreEqual("cyborg.tests.description.v1", root.GetProperty("ModuleId").GetString());
        Assert.IsTrue(root.GetProperty("Options").GetProperty("Enabled").GetBoolean());
        Assert.AreEqual("first", root.GetProperty("Items")[0].GetString());
        Assert.AreEqual(42, root.GetProperty("Items")[1].GetProperty("Value").GetInt32());
    }

    private sealed class TestDescriptor : IModuleDescriptor
    {
        public void Describe(IObjectDescriptionBuilder builder)
        {
            builder.AddProperty("ModuleId", [], "cyborg.tests.description.v1");
            builder.AddObject("Options", [], options =>
            {
                options.AddProperty("Enabled", [], true);
            });
            builder.AddCollection("Items", [], items =>
            {
                items.AddItem([], "first");
                items.AddObjectItem([], item =>
                {
                    item.AddProperty("Value", [], 42);
                });
            });
        }
    }
}
