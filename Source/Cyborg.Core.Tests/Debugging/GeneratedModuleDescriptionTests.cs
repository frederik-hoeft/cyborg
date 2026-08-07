using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Descriptors;
using Cyborg.TestModules.Description;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;
using System.Text.Json;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class GeneratedModuleDescriptionTests
{
    private DescriptionTestServiceProvider _services = null!;
    private IModuleSerializationService _serializationService = null!;

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Initialize()
    {
        _services = new DescriptionTestServiceProvider();
        _serializationService = _services.GetRequiredService<IModuleSerializationService>();
    }

    [TestMethod]
    public async Task ToJsonAsync_StringProperty_IsScalarAsync()
    {
        GeneratedDescriptionTestModule module = new()
        {
            Text = "hello",
            OptionalText = "optional",
            Marker = 'x',
            Values = ["first", "second"],
        };

        string json = await _serializationService.ToJsonAsync(module, TestContext.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement root = document.RootElement;
        Assert.AreEqual(JsonValueKind.String, root.GetProperty(nameof(module.Text)).ValueKind);
        Assert.AreEqual("hello", root.GetProperty(nameof(module.Text)).GetString());
        Assert.AreEqual(
            JsonValueKind.String,
            root.GetProperty(nameof(module.OptionalText)).ValueKind);
        Assert.AreEqual(
            "optional",
            root.GetProperty(nameof(module.OptionalText)).GetString());
        Assert.AreEqual("x", root.GetProperty(nameof(module.Marker)).GetString());
        Assert.AreEqual(JsonValueKind.Array, root.GetProperty(nameof(module.Values)).ValueKind);
        Assert.AreEqual("first", root.GetProperty(nameof(module.Values))[0].GetString());
    }

    [TestMethod]
    public async Task ToJsonAsync_NestedObjectAndCollection_AreRecursiveAsync()
    {
        GeneratedDescriptionTestModule module = new()
        {
            Text = "root",
            Child = new DescriptionTestChild { Value = "child" },
            Children =
            [
                new DescriptionTestChild { Value = "first" },
                new DescriptionTestChild { Value = "second" },
            ],
            Values = ["value"],
        };

        string json = await _serializationService.ToJsonAsync(module, TestContext.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement root = document.RootElement;
        Assert.AreEqual(
            "child",
            root.GetProperty(nameof(module.Child)).GetProperty(nameof(DescriptionTestChild.Value)).GetString());
        Assert.AreEqual(
            "second",
            root.GetProperty(nameof(module.Children))[1].GetProperty(nameof(DescriptionTestChild.Value)).GetString());
    }

    [TestMethod]
    public async Task ToJsonAsync_NullAndNullableCollections_PreserveShapeAsync()
    {
        GeneratedDescriptionTestModule module = new()
        {
            Text = "root",
            ArrayValues = ["array-value"],
            OptionalValues = null,
            NullableChildren =
            [
                null,
                new DescriptionTestChild { Value = "present" },
            ],
            OptionalImmutableValues = ImmutableArray<string>.Empty,
            Values = ["value"],
        };

        string json = await _serializationService.ToJsonAsync(module, TestContext.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement root = document.RootElement;
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty(nameof(module.Child)).ValueKind);
        Assert.AreEqual(
            JsonValueKind.Null,
            root.GetProperty(nameof(module.OptionalValues)).ValueKind);
        Assert.AreEqual(
            "array-value",
            root.GetProperty(nameof(module.ArrayValues))[0].GetString());
        Assert.AreEqual(
            JsonValueKind.Null,
            root.GetProperty(nameof(module.NullableChildren))[0].ValueKind);
        Assert.AreEqual(
            "present",
            root.GetProperty(nameof(module.NullableChildren))[1]
                .GetProperty(nameof(DescriptionTestChild.Value))
                .GetString());
        Assert.AreEqual(
            0,
            root.GetProperty(nameof(module.OptionalImmutableValues)).GetArrayLength());
    }

    [TestMethod]
    public async Task ToTextAsync_EmptyImmutableArray_RendersEmptyCollectionAsync()
    {
        GeneratedDescriptionTestModule module = new()
        {
            Text = "root",
            Values = [],
        };

        string text = await _serializationService.ToTextAsync(module, TestContext.CancellationToken);

        Assert.Contains($"{nameof(module.Values)}:{Environment.NewLine}  (empty)", text);
    }

    [TestMethod]
    public async Task ToTextAsync_DefaultImmutableArray_DoesNotEnumerateAsync()
    {
        GeneratedDescriptionTestModule module = new()
        {
            Text = "root",
            Values = default,
        };

        string text = await _serializationService.ToTextAsync(module, TestContext.CancellationToken);

        Assert.Contains($"{nameof(module.Values)}:", text);
        Assert.DoesNotContain($"{nameof(module.Values)}: (empty)", text);
    }
}

