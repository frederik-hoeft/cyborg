using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Validation;
using Cyborg.Core.TestAdapter;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;

namespace Cyborg.Modules.Tests.Core.Validation;

[TestClass]
public sealed class ValidationPipelineRegressionTests : ModuleTestBase
{
    [TestMethod]
    public async Task TestValidationAsync_DefaultRequiredImmutableArray_ProducesRequiredErrorWithoutEnumeratingAsync()
    {
        ValidationPipelineTestModule module = new(
            RequiredItems: default,
            OptionalItems: [],
            NullableItems: null,
            InterpolatedValue: "literal",
            DeferredValue: "literal");

        await using TestModuleRuntimeScope scope = CreateValidationScope();
        ValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(
            scope.Runtime,
            scope.ServiceProvider,
            TestContext.CancellationToken);

        MSAssert.IsFalse(result.IsValid);
        MSAssert.Contains(
            error => error.Rule == "required"
                && error.PropertyName.EndsWith(nameof(ValidationPipelineTestModule.RequiredItems), StringComparison.Ordinal),
            result.Errors);
    }

    [TestMethod]
    public async Task TestValidationAsync_DefaultOptionalImmutableArray_RemainsDefaultAsync()
    {
        ValidationPipelineTestModule module = new(
            RequiredItems: [],
            OptionalItems: default,
            NullableItems: null,
            InterpolatedValue: "literal",
            DeferredValue: "literal");

        await using TestModuleRuntimeScope scope = CreateValidationScope();
        ValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(
            scope.Runtime,
            scope.ServiceProvider,
            TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.IsTrue(result.Module.OptionalItems.IsDefault);
    }

    [TestMethod]
    public async Task TestValidationAsync_NullableImmutableArrayContainingDefault_RemainsDefaultAsync()
    {
        ValidationPipelineTestModule module = new(
            RequiredItems: [],
            OptionalItems: [],
            NullableItems: (ImmutableArray<ValidationPipelineTestItem>?)default(ImmutableArray<ValidationPipelineTestItem>),
            InterpolatedValue: "literal",
            DeferredValue: "literal");

        await using TestModuleRuntimeScope scope = CreateValidationScope();
        ValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(
            scope.Runtime,
            scope.ServiceProvider,
            TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.IsTrue(result.Module.NullableItems.HasValue);
        MSAssert.IsTrue(result.Module.NullableItems.Value.IsDefault);
    }

    [TestMethod]
    public async Task TestValidationAsync_CollectionElementDefaultsAreAppliedBeforeInterpolationAsync()
    {
        ValidationPipelineTestModule module = new(
            RequiredItems: [new(Value: null)],
            OptionalItems: [],
            NullableItems: null,
            InterpolatedValue: "literal",
            DeferredValue: "literal");

        await using TestModuleRuntimeScope scope = CreateValidationScope();
        scope.GlobalEnvironment.SetVariable("fallback", "resolved-default");
        ValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(
            scope.Runtime,
            scope.ServiceProvider,
            TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("resolved-default", result.Module.RequiredItems[0].Value);
    }

    [TestMethod]
    public async Task TestValidationAsync_StructuralAndDeferredStringsAreNotInterpolatedAsync()
    {
        ValidationPipelineTestModule module = new(
            RequiredItems: [],
            OptionalItems: [],
            NullableItems: null,
            InterpolatedValue: "${resolved}",
            DeferredValue: "${deferred}")
        {
            Name = "${name}",
            Group = "${group}",
        };

        await using TestModuleRuntimeScope scope = CreateValidationScope();
        scope.GlobalEnvironment.SetVariable("resolved", "resolved-value");
        scope.GlobalEnvironment.SetVariable("deferred", "deferred-value");
        scope.GlobalEnvironment.SetVariable("name", "resolved-name");
        scope.GlobalEnvironment.SetVariable("group", "resolved-group");
        ValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(
            scope.Runtime,
            scope.ServiceProvider,
            TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("resolved-value", result.Module.InterpolatedValue);
        MSAssert.AreEqual("${deferred}", result.Module.DeferredValue);
        MSAssert.AreEqual("${name}", result.Module.Name);
        MSAssert.AreEqual("${group}", result.Module.Group);
    }

    private TestModuleRuntimeScope CreateValidationScope()
    {
        IServiceCollection services = TestServiceConfiguration.CreateDefaultServices();
        ConfigureServices(services, new JabServiceDiscovery());
        return TestModuleRuntimeScope.Create(services);
    }
}

[GeneratedModuleValidation]
public sealed partial record ValidationPipelineTestModule
(
    [property: Required] ImmutableArray<ValidationPipelineTestItem> RequiredItems,
    ImmutableArray<ValidationPipelineTestItem> OptionalItems,
    ImmutableArray<ValidationPipelineTestItem>? NullableItems,
    string InterpolatedValue,
    [property: IgnoreInterpolation] string DeferredValue
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.tests.validation-pipeline.v1";
}

[Validatable]
public sealed record ValidationPipelineTestItem
(
    [property: Required]
    [property: DefaultValue<string>("${fallback}")]
    string? Value
);
