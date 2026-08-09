using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Validation;
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
                && error.PropertyName.EndsWith(nameof(ValidationPipelineTestModule.Tags), StringComparison.Ordinal),
            result.Errors);
        MSAssert.Contains(
            error => error.Rule == "valid_identifier"
                && error.PropertyName.EndsWith(nameof(ValidationPipelineTestModule.Tags), StringComparison.Ordinal),
            result.Errors);
        MSAssert.Contains(
            error => error.Rule == "required"
                && error.Message.Equals("Collection element 0 of property 'Tags' is required.", StringComparison.Ordinal),
            result.Errors);
        MSAssert.Contains(
            error => error.Rule == "valid_identifier"
                && error.Message.Equals("Collection element 2 of property 'Tags' must be a valid variable identifier, but was 'invalid tag'.", StringComparison.Ordinal),
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
}
