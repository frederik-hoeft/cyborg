using Cyborg.Core.Modules.Descriptors;
using Cyborg.TestModules.Description;
using Cyborg.TestModules.Validation;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;
using System.Text.Json;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class GeneratedModuleDescriptionTests : CyborgCoreTestBase
{
    [TestMethod]
    public Task Test_ToJsonAsync_StringProperty_IsScalarAsync() => TestWithDIAsync(async services =>
    {
        IModuleSerializationService serializationService = services.GetRequiredService<IModuleSerializationService>();

        GeneratedDescriptionTestModule module = new()
        {
            Text = "hello",
            OptionalText = "optional",
            Marker = 'x',
            Values = ["first", "second"],
        };

        string json = await serializationService.ToJsonAsync(module, TestContext.CancellationToken);
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
    });

    [TestMethod]
    public Task Test_ToJsonAsync_NestedObjectAndCollection_AreRecursiveAsync() => TestWithDIAsync(async services =>
    {
        IModuleSerializationService serializationService = services.GetRequiredService<IModuleSerializationService>();
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

        string json = await serializationService.ToJsonAsync(module, TestContext.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement root = document.RootElement;
        Assert.AreEqual(
            "child",
            root.GetProperty(nameof(module.Child)).GetProperty(nameof(DescriptionTestChild.Value)).GetString());
        Assert.AreEqual(
            "second",
            root.GetProperty(nameof(module.Children))[1].GetProperty(nameof(DescriptionTestChild.Value)).GetString());
    });

    [TestMethod]
    public Task Test_ToJsonAsync_ValidatableRecordStructs_PreservePresenceSemanticsAsync() => TestWithDIAsync(async services =>
    {
        IModuleSerializationService serializationService = services.GetRequiredService<IModuleSerializationService>();
        ValidationPipelineTestModule module = new(
            RequiredItems: [],
            OptionalItems: [],
            NullableItems: null,
            InterpolatedValue: "value",
            DeferredValue: "value",
            Tags: null)
        {
            ValueItem = new("present"),
            NullableValueItem = null,
        };

        string json = await serializationService.ToJsonAsync(module, TestContext.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement root = document.RootElement;
        Assert.AreEqual(
            "present",
            root.GetProperty(nameof(module.ValueItem)).GetProperty(nameof(ValidationPipelineValueItem.Value)).GetString());
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty(nameof(module.NullableValueItem)).ValueKind);
    });

    [TestMethod]
    public Task Test_ToJsonAsync_NullAndNullableCollections_PreserveShapeAsync() => TestWithDIAsync(async services =>
    {
        IModuleSerializationService serializationService = services.GetRequiredService<IModuleSerializationService>();
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

        string json = await serializationService.ToJsonAsync(module, TestContext.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement root = document.RootElement;
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty(nameof(module.Child)).ValueKind);
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty(nameof(module.OptionalValues)).ValueKind);
        Assert.AreEqual("array-value", root.GetProperty(nameof(module.ArrayValues))[0].GetString());
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty(nameof(module.NullableChildren))[0].ValueKind);
        Assert.AreEqual("present", root.GetProperty(nameof(module.NullableChildren))[1]
            .GetProperty(nameof(DescriptionTestChild.Value))
            .GetString());
        Assert.AreEqual(0, root.GetProperty(nameof(module.OptionalImmutableValues)).GetArrayLength());
    });

    [TestMethod]
    public Task Test_ToTextAsync_EmptyImmutableArray_RendersEmptyCollectionAsync() => TestWithDIAsync(async services =>
    {
        IModuleSerializationService serializationService = services.GetRequiredService<IModuleSerializationService>();
        GeneratedDescriptionTestModule module = new()
        {
            Text = "root",
            Values = [],
        };

        string text = await serializationService.ToTextAsync(module, TestContext.CancellationToken);

        Assert.Contains($"{nameof(module.Values)}:{Environment.NewLine}  (empty)", text);
    });

    [TestMethod]
    public Task Test_ToTextAsync_DefaultImmutableArray_DoesNotEnumerateAsync() => TestWithDIAsync(async services =>
    {
        IModuleSerializationService serializationService = services.GetRequiredService<IModuleSerializationService>();
        GeneratedDescriptionTestModule module = new()
        {
            Text = "root",
            Values = default,
        };

        string text = await serializationService.ToTextAsync(module, TestContext.CancellationToken);

        Assert.Contains($"{nameof(module.Values)}:", text);
        Assert.DoesNotContain($"{nameof(module.Values)}: (empty)", text);
    });
}
