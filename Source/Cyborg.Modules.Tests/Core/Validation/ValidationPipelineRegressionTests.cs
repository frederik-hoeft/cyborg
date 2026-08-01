using Cyborg.Core.Modules.Validation;
using Cyborg.Core.TestAdapter;
using Cyborg.TestModules.Validation;
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
        ValidationPipelineTestModule validatedModule = result.Module!;
        MSAssert.IsTrue(validatedModule.OptionalItems.IsDefault);
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
        ValidationPipelineTestModule validatedModule = result.Module!;
        MSAssert.IsTrue(validatedModule.NullableItems.HasValue);
        MSAssert.IsTrue(validatedModule.NullableItems.Value.IsDefault);
    }

    [TestMethod]
    public async Task TestValidationAsync_CollectionElementDefaultsAreAppliedBeforeInterpolationAsync()
    {
        ValidationPipelineTestModule module = new(
            RequiredItems: [new(Value: null!)],
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
        ValidationPipelineTestModule validatedModule = result.Module!;
        MSAssert.AreEqual("resolved-default", validatedModule.RequiredItems[0].Value);
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
        ValidationPipelineTestModule validatedModule = result.Module!;
        MSAssert.AreEqual("resolved-value", validatedModule.InterpolatedValue);
        MSAssert.AreEqual("${deferred}", validatedModule.DeferredValue);
        MSAssert.AreEqual("${name}", validatedModule.Name);
        MSAssert.AreEqual("${group}", validatedModule.Group);
    }

    private TestModuleRuntimeScope CreateValidationScope()
    {
        IServiceCollection services = TestServiceConfiguration.CreateDefaultServices();
        ConfigureServices(services, new JabServiceDiscovery());
        return TestModuleRuntimeScope.Create(services);
    }
}
