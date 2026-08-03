using Cyborg.Core.Modules.Validation;
using Cyborg.Core.TestAdapter;
using Cyborg.TestModules.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Modules.Tests.Core.Validation;

[TestClass]
public sealed class RepeatedCollectionValidationTests : ModuleTestBase
{
    [TestMethod]
    public async Task TestValidationAsync_RepeatedRequiredAttributesReportParentErrorAsync()
    {
        ValidationPipelineTestModule module = CreateModule() with
        {
            RequiredTags = null,
        };

        await using TestModuleRuntimeScope scope = CreateValidationScope();
        ValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(
            scope.Runtime,
            scope.ServiceProvider,
            TestContext.CancellationToken);

        MSAssert.HasCount(1, result.Errors);
        MSAssert.Contains(
            error => error.Rule == "required"
                && error.PropertyName.EndsWith(nameof(ValidationPipelineTestModule.RequiredTags), StringComparison.Ordinal),
            result.Errors);
    }

    [TestMethod]
    public async Task TestValidationAsync_RepeatedRequiredAttributesReportElementErrorAsync()
    {
        ValidationPipelineTestModule module = CreateModule() with
        {
            RequiredTags = [null],
        };

        await using TestModuleRuntimeScope scope = CreateValidationScope();
        ValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(
            scope.Runtime,
            scope.ServiceProvider,
            TestContext.CancellationToken);

        MSAssert.HasCount(1, result.Errors);
        MSAssert.Contains(
            error => error.Rule == "required"
                && error.PropertyName.EndsWith(nameof(ValidationPipelineTestModule.RequiredTags), StringComparison.Ordinal),
            result.Errors);
    }

    private static ValidationPipelineTestModule CreateModule() => new(
        RequiredItems: [],
        OptionalItems: [],
        NullableItems: null,
        InterpolatedValue: "literal",
        DeferredValue: "literal",
        Tags: null);

    private TestModuleRuntimeScope CreateValidationScope()
    {
        IServiceCollection services = TestServiceConfiguration.CreateDefaultServices();
        ConfigureServices(services, new JabServiceDiscovery());
        return TestModuleRuntimeScope.Create(services);
    }
}
