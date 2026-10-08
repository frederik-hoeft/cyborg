using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Validation;
using Cyborg.TestModules.Validation;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;

namespace Cyborg.Modules.Tests.Core.Validation;

[TestClass]
public sealed class ValidationPipelineRegressionTests : ModuleTestBase
{
    [TestMethod]
    public Task TestValidationAsync_DefaultRequiredImmutableArray_ProducesRequiredErrorWithoutEnumeratingAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = new(
            RequiredItems: default,
            OptionalItems: [],
            NullableItems: null,
            InterpolatedValue: "literal",
            DeferredValue: "literal",
            Tags: null);

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsFalse(result.IsValid);
        MSAssert.IsTrue(result.Module.RequiredItems.IsDefault);
        MSAssert.Contains(
            error => error.Rule == "required"
                && error.PropertyName.EndsWith(nameof(ValidationPipelineTestModule.RequiredItems), StringComparison.Ordinal),
            result.Errors);
    });

    [TestMethod]
    public Task TestValidationAsync_DefaultOptionalImmutableArray_RemainsDefaultAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = new(
            RequiredItems: [],
            OptionalItems: default,
            NullableItems: null,
            InterpolatedValue: "literal",
            DeferredValue: "literal",
            Tags: null);

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        ValidationPipelineTestModule validatedModule = result.Module;
        MSAssert.IsTrue(validatedModule.OptionalItems.IsDefault);
    });

    [TestMethod]
    public Task TestValidationAsync_NullableImmutableArrayContainingDefault_RemainsDefaultAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = new(
            RequiredItems: [],
            OptionalItems: [],
            NullableItems: (ImmutableArray<ValidationPipelineTestItem>?)default(ImmutableArray<ValidationPipelineTestItem>),
            InterpolatedValue: "literal",
            DeferredValue: "literal",
            Tags: null);

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        ValidationPipelineTestModule validatedModule = result.Module;
        MSAssert.IsTrue(validatedModule.NullableItems.HasValue);
        MSAssert.IsTrue(validatedModule.NullableItems.Value.IsDefault);
    });

    [TestMethod]
    public Task TestValidationAsync_DefaultImmutableArray_SkipsLengthValidationWithoutEnumeratingAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = CreateValidModule() with
        {
            LengthCheckedItems = default,
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.IsTrue(result.Module.LengthCheckedItems.IsDefault);
    });

    [TestMethod]
    public Task TestValidationAsync_EmptyImmutableArray_RunsLengthValidationAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = CreateValidModule() with
        {
            LengthCheckedItems = [],
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsFalse(result.IsValid);
        MSAssert.Contains(
            error => error.Rule == "length"
                && error.PropertyName.EndsWith(nameof(ValidationPipelineTestModule.LengthCheckedItems), StringComparison.Ordinal),
            result.Errors);
    });

    [TestMethod]
    public Task TestValidationAsync_NullableImmutableArrayContainingDefault_SkipsLengthValidationWithoutEnumeratingAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = CreateValidModule() with
        {
            NullableLengthCheckedItems = (ImmutableArray<string>?)default(ImmutableArray<string>),
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.IsTrue(result.Module.NullableLengthCheckedItems.HasValue);
        MSAssert.IsTrue(result.Module.NullableLengthCheckedItems.Value.IsDefault);
    });

    [TestMethod]
    public Task TestValidationAsync_NullableImmutableArrayContainingDefault_FailsRequiredAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = CreateValidModule() with
        {
            RequiredNullableImmutableItems = (ImmutableArray<string>?)default(ImmutableArray<string>),
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsFalse(result.IsValid);
        MSAssert.Contains(
            error => error.Rule == "required"
                && error.PropertyName.EndsWith(nameof(ValidationPipelineTestModule.RequiredNullableImmutableItems), StringComparison.Ordinal),
            result.Errors);
    });

    [TestMethod]
    public Task TestValidationAsync_ArrayLengthValidation_UsesSupportedCollectionShapeAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = CreateValidModule() with
        {
            ArrayLengthCheckedItems = [],
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsFalse(result.IsValid);
        MSAssert.Contains(
            error => error.Rule == "length"
                && error.PropertyName.EndsWith(nameof(ValidationPipelineTestModule.ArrayLengthCheckedItems), StringComparison.Ordinal),
            result.Errors);
    });

    [TestMethod]
    public Task TestValidationAsync_NestedNullableImmutableArrayLength_UsesSharedCollectionAccessSemanticsAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = CreateValidModule() with
        {
            NestedLengthItems =
            [
                (ImmutableArray<string>?)default(ImmutableArray<string>),
                [],
                ["value"],
                null,
            ],
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsFalse(result.IsValid);
        MSAssert.ContainsSingle(
            error => error.Rule == "length"
                && error.PropertyName.Equals("NestedLengthItems[1]", StringComparison.Ordinal)
                && error.Message.StartsWith("Property 'NestedLengthItems[1]'", StringComparison.Ordinal),
            result.Errors);
    });

    [TestMethod]
    public Task TestValidationAsync_ValidatableObjects_UseSharedObjectAccessSemanticsAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.Environment.SetVariable("fallback", "resolved-default");

        ValidationPipelineTestModule module = CreateValidModule() with
        {
            ReferenceItem = new(Value: null!),
            NullableReferenceItem = new(Value: null!),
            ValueItem = default,
            NullableValueItem = new(Value: null!),
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("resolved-default", result.Module.ReferenceItem.Value);
        MSAssert.AreEqual("resolved-default", result.Module.NullableReferenceItem!.Value);
        MSAssert.AreEqual("resolved-default", result.Module.ValueItem.Value);
        MSAssert.AreEqual("resolved-default", result.Module.NullableValueItem!.Value.Value);
    });

    [TestMethod]
    public Task TestValidationAsync_AbsentNullableValidatableObjects_RemainAbsentAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.Environment.SetVariable("fallback", "resolved-default");

        ValidationPipelineTestModule module = CreateValidModule() with
        {
            NullableReferenceItem = null,
            NullableValueItem = null,
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.IsNull(result.Module.NullableReferenceItem);
        MSAssert.IsFalse(result.Module.NullableValueItem.HasValue);
    });

    [TestMethod]
    public Task TestValidationAsync_NullableValidatableCollectionElements_UseSharedElementAccessSemanticsAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.Environment.SetVariable("fallback", "resolved-default");

        ValidationPipelineTestModule module = CreateValidModule() with
        {
            NullableElementItems = [null, new(Value: null!)],
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.IsNull(result.Module.NullableElementItems[0]);
        MSAssert.AreEqual("resolved-default", result.Module.NullableElementItems[1]!.Value);
    });

    [TestMethod]
    public Task TestValidationAsync_NullableValueTypeCollectionElements_UseSharedElementAccessSemanticsAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.Environment.SetVariable("fallback", "resolved-default");

        ValidationPipelineTestModule module = CreateValidModule() with
        {
            NullableValueElementItems = [null, new(Value: null!)],
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.IsFalse(result.Module.NullableValueElementItems[0].HasValue);
        MSAssert.AreEqual("resolved-default", result.Module.NullableValueElementItems[1]!.Value.Value);
    });

    [TestMethod]
    public Task TestValidationAsync_ValueTypeCollectionMaterialization_PreservesRewrittenElementsAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.Environment.SetVariable("fallback", "resolved-default");

        ValidationPipelineStructCollection<ValidationPipelineTestItem> items = [new(Value: null!)];
        ValidationPipelineTestModule module = CreateValidModule() with
        {
            StructCollectionItems = items,
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("resolved-default", result.Module.StructCollectionItems.Single().Value);
    });

    [TestMethod]
    public Task TestValidationAsync_CollectionElementDefaultsAreAppliedBeforeInterpolationAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = new(
            RequiredItems: [new(Value: null!)],
            OptionalItems: [],
            NullableItems: null,
            InterpolatedValue: "literal",
            DeferredValue: "literal",
            Tags: null);

        runtime.Environment.SetVariable("fallback", "resolved-default");
        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        ValidationPipelineTestModule validatedModule = result.Module;
        MSAssert.AreEqual("resolved-default", validatedModule.RequiredItems[0].Value);
    });

    [TestMethod]
    public Task TestValidationAsync_DirectLazyStringIndirectionResolvesBeforeInterpolationAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.Environment.SetVariable("resolved", "resolved-value");
        ValidationPipelineTestModule module = CreateValidModule() with
        {
            InterpolatedValue = "&{resolved}",
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("resolved-value", result.Module.InterpolatedValue);
    });

    [TestMethod]
    public Task TestValidationAsync_IgnoreInterpolationStillResolvesLazyIndirectionAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.Environment.SetVariable("resolved", "resolved-value");
        ValidationPipelineTestModule module = CreateValidModule() with
        {
            DeferredValue = "&{resolved}",
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("resolved-value", result.Module.DeferredValue);
    });

    [TestMethod]
    public Task TestValidationAsync_EscapedLazyIndirectionFinalizesWithoutEvaluationAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.Environment.SetVariable("resolved", "resolved-value");
        ValidationPipelineTestModule module = CreateValidModule() with
        {
            InterpolatedValue = "&{#resolved}",
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("&{resolved}", result.Module.InterpolatedValue);
    });

    [TestMethod]
    public Task TestValidationAsync_IgnoreInterpolationFinalizesEscapedLazyIndirectionAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.Environment.SetVariable("resolved", "resolved-value");
        ValidationPipelineTestModule module = CreateValidModule() with
        {
            DeferredValue = "&{#resolved}",
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("&{resolved}", result.Module.DeferredValue);
    });

    [TestMethod]
    public Task TestValidationAsync_InterpolationOperandCanFollowLazyIndirectionAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.Environment.SetVariable("source", "world");
        runtime.Environment.SetVariable("alias", "&{source}");
        ValidationPipelineTestModule module = CreateValidModule() with
        {
            InterpolatedValue = "Hello ${alias}",
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("Hello world", result.Module.InterpolatedValue);
    });

    [TestMethod]
    public Task TestValidationAsync_DeferredStringsAreNotInterpolatedAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = new(
            RequiredItems: [],
            OptionalItems: [],
            NullableItems: null,
            InterpolatedValue: "${resolved}",
            DeferredValue: "${deferred}",
            Tags: null);

        runtime.Environment.SetVariable("resolved", "resolved-value");
        runtime.Environment.SetVariable("deferred", "deferred-value");
        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        ValidationPipelineTestModule validatedModule = result.Module;
        MSAssert.AreEqual("resolved-value", validatedModule.InterpolatedValue);
        MSAssert.AreEqual("${deferred}", validatedModule.DeferredValue);
    });

    [TestMethod]
    public Task TestValidationAsync_IgnoreInterpolationPreservesDefaultExpressionAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = new(
            RequiredItems: [],
            OptionalItems: [],
            NullableItems: null,
            InterpolatedValue: "literal",
            DeferredValue: "literal",
            Tags: null);

        runtime.Environment.SetVariable("deferred_default", "resolved-value");
        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("${deferred_default}", result.Module.DeferredDefault);
    });

    [TestMethod]
    public Task TestValidationAsync_NormalStringOverrideIsInterpolatedDuringFinalPhaseAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = new(
            RequiredItems: [],
            OptionalItems: [],
            NullableItems: null,
            InterpolatedValue: "fallback",
            DeferredValue: "literal",
            Tags: null)
        {
            Name = "validation",
        };

        runtime.Environment.SetVariable("@validation.interpolated_value", "${resolved}");
        runtime.Environment.SetVariable("resolved", "resolved-value");
        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("resolved-value", result.Module.InterpolatedValue);
    });

    [TestMethod]
    public Task TestValidationAsync_NormalStringOverrideResolvesLazyIndirectionBeforeInterpolationAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ValidationPipelineTestModule module = CreateValidModule() with
        {
            Name = "validation",
        };
        runtime.Environment.SetVariable("resolved", "resolved-value");
        runtime.Environment.SetVariable("@validation.interpolated_value", "&{resolved}");

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("resolved-value", result.Module.InterpolatedValue);
    });

    [TestMethod]
    public Task TestValidationAsync_IgnoreInterpolationOverrideStillResolvesLazyIndirectionAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ValidationPipelineTestModule module = CreateValidModule() with
        {
            Name = "validation",
        };
        runtime.Environment.SetVariable("resolved", "resolved-value");
        runtime.Environment.SetVariable("@validation.deferred_value", "&{resolved}");

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("resolved-value", result.Module.DeferredValue);
    });

    [TestMethod]
    public Task TestValidationAsync_IgnoreInterpolationPreservesOverrideExpressionAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = new(
            RequiredItems: [],
            OptionalItems: [],
            NullableItems: null,
            InterpolatedValue: "literal",
            DeferredValue: "fallback",
            Tags: null)
        {
            Name = "validation",
        };

        runtime.Environment.SetVariable("@validation.deferred_value", "${deferred}");
        runtime.Environment.SetVariable("deferred", "resolved-value");
        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("${deferred}", result.Module.DeferredValue);
    });

    [TestMethod]
    public Task TestValidationAsync_CapturedTextOverrideSurvivesGeneratedInterpolationAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ValidationPipelineTestModule module = new(
            RequiredItems: [],
            OptionalItems: [],
            NullableItems: null,
            InterpolatedValue: "fallback",
            DeferredValue: "fallback",
            Tags: null)
        {
            Name = "validation",
        };
        runtime.Environment.SetVariable("template", "${#name}");
        runtime.Environment.SetVariable("name", "resolved-name");
        runtime.Environment.SetVariable("snapshot", "*{template}");
        runtime.Environment.SetVariable("@validation.interpolated_value", "*{snapshot}");
        runtime.Environment.SetVariable("@validation.deferred_value", "*{snapshot}");

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("${name}", result.Module.InterpolatedValue);
        MSAssert.AreEqual("${name}", result.Module.DeferredValue);
    });

    [TestMethod]
    public Task TestValidationAsync_CapturedCollectionOverrideKeepsElementPreparationAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ValidationPipelineTestModule module = CreateValidModule() with
        {
            Name = "validation",
        };
        IReadOnlyCollection<string?> source = ["${tag}"];
        runtime.Environment.SetVariable("source_tags", source);
        runtime.Environment.SetVariable("captured_tags", "*{source_tags}");
        runtime.Environment.SetVariable("@validation.tags", "*{captured_tags}");
        runtime.Environment.SetVariable("tag", "resolved-tag");

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("resolved-tag", result.Module.Tags!.Single());
    });

    [TestMethod]
    public Task TestValidationAsync_CapturedCollectionOverridePreservesNullElementsForValidationAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ValidationPipelineTestModule module = CreateValidModule() with
        {
            Name = "validation",
        };
        IReadOnlyCollection<string?> source = [null];
        runtime.Environment.SetVariable("source_tags", source);
        runtime.Environment.SetVariable("captured_tags", "*{source_tags}");
        runtime.Environment.SetVariable("@validation.tags", "*{captured_tags}");

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsFalse(result.IsValid);
        MSAssert.Contains(error => error.Rule == "required" && error.PropertyName.Equals("Tags[0]", StringComparison.Ordinal), result.Errors);
    });

    [TestMethod]
    public Task TestValidationAsync_InterpolatedIdentifiersAreRejectedAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = new(
            RequiredItems: [],
            OptionalItems: [],
            NullableItems: null,
            InterpolatedValue: "${resolved}",
            DeferredValue: "${deferred}",
            Tags: null)
        {
            Name = "${name}",
            Group = "${group}",
        };

        runtime.Environment.SetVariable("resolved", "resolved-value");
        runtime.Environment.SetVariable("deferred", "deferred-value");
        runtime.Environment.SetVariable("name", "resolved-name");
        runtime.Environment.SetVariable("group", "resolved-group");
        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.HasCount(2, result.Errors);
        MSAssert.Contains(
            error => error.Rule == "valid_identifier"
                && error.PropertyName.EndsWith(nameof(ValidationPipelineTestModule.Name), StringComparison.Ordinal),
            result.Errors);
        MSAssert.Contains(
            error => error.Rule == "valid_identifier"
                && error.PropertyName.EndsWith(nameof(ValidationPipelineTestModule.Group), StringComparison.Ordinal),
            result.Errors);
    });

    [TestMethod]
    public Task TestValidationAsync_ValueExpressionIdentifiersAreNotPreparedAfterBindingAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.Environment.SetVariable("name", "resolved-name");
        runtime.Environment.SetVariable("group", "resolved-group");
        ValidationPipelineTestModule module = CreateValidModule() with
        {
            Name = "&{name}",
            Group = "&{group}",
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.HasCount(2, result.Errors);
        MSAssert.AreEqual("&{name}", result.Module.Name);
        MSAssert.AreEqual("&{group}", result.Module.Group);
    });

    [TestMethod]
    public Task TestValidationAsync_CollectionElementConstraintsAcceptValidTagsAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = new(
            RequiredItems: [],
            OptionalItems: [],
            NullableItems: null,
            InterpolatedValue: "literal",
            DeferredValue: "literal",
            Tags: ["primary-tag", "group.0"]);

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
    });

    [TestMethod]
    public Task TestValidationAsync_CollectionElementConstraintsAcceptParentViolationsAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = new(
            RequiredItems: [],
            OptionalItems: [],
            NullableItems: null,
            InterpolatedValue: "literal",
            DeferredValue: "literal",
            Tags: null);

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
    });

    [TestMethod]
    public Task TestValidationAsync_CollectionElementConstraintsReportElementErrorsAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = new(
            RequiredItems: [],
            OptionalItems: [],
            NullableItems: null,
            InterpolatedValue: "literal",
            DeferredValue: "literal",
            Tags: [null, string.Empty, "invalid tag"]);

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsFalse(result.IsValid);
        MSAssert.HasCount(4, result.Errors);
        MSAssert.Contains(
            error => error.Rule == "required"
                && error.PropertyName.Equals("Tags[0]", StringComparison.Ordinal)
                && error.Message.Equals("Property 'Tags[0]' is required.", StringComparison.Ordinal),
            result.Errors);
        MSAssert.Contains(
            error => error.Rule == "valid_identifier"
                && error.PropertyName.Equals("Tags[2]", StringComparison.Ordinal)
                && error.Message.Equals("Property 'Tags[2]' must be a valid variable identifier, but was 'invalid tag'.", StringComparison.Ordinal),
            result.Errors);
    });

    [TestMethod]
    public Task TestValidationAsync_NestedValidationErrorsExposeRecursivePathsAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = CreateValidModule() with
        {
            ValidationPathItems =
            [
                new ValidationPathTestItem(
                    Value: null,
                    Values: ["valid", null]),
            ],
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsFalse(result.IsValid);
        MSAssert.HasCount(2, result.Errors);
        MSAssert.Contains(
            error => error.Rule == "required"
                && error.PropertyName.Equals("ValidationPathItems[0].Value", StringComparison.Ordinal)
                && error.Message.Equals("Property 'ValidationPathItems[0].Value' is required.", StringComparison.Ordinal),
            result.Errors);
        MSAssert.Contains(
            error => error.Rule == "required"
                && error.PropertyName.Equals("ValidationPathItems[0].Values[1]", StringComparison.Ordinal)
                && error.Message.Equals("Property 'ValidationPathItems[0].Values[1]' is required.", StringComparison.Ordinal),
            result.Errors);
    });

    [TestMethod]
    public Task TestValidationAsync_DeepNestedValidationWorkIsNotPrunedAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = CreateValidModule() with
        {
            RecursiveValidationPathItems =
            [
                new ValidationPathContainerItem(
                    new ValidationPathTestItem(
                        Value: null,
                        Values: [])),
            ],
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsFalse(result.IsValid);
        MSAssert.ContainsSingle(
            error => error.Rule == "required"
                && error.PropertyName.Equals("RecursiveValidationPathItems[0].Child.Value", StringComparison.Ordinal)
                && error.Message.Equals("Property 'RecursiveValidationPathItems[0].Child.Value' is required.", StringComparison.Ordinal),
            result.Errors);
    });

    [TestMethod]
    public Task TestValidationAsync_CollectionElementConstraintsRunAfterInterpolationAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = new(
            RequiredItems: [],
            OptionalItems: [],
            NullableItems: null,
            InterpolatedValue: "literal",
            DeferredValue: "literal",
            Tags: ["${tag}"]);

        runtime.GlobalEnvironment.SetVariable("tag", "resolved-tag");
        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("resolved-tag", result.Module!.Tags!.Single());
    });

    private static ValidationPipelineTestModule CreateValidModule() => new(
        RequiredItems: [],
        OptionalItems: [],
        NullableItems: null,
        InterpolatedValue: "literal",
        DeferredValue: "literal",
        Tags: null);
}
