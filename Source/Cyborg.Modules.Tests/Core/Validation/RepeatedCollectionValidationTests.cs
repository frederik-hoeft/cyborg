using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Validation;
using Cyborg.TestModules.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Modules.Tests.Core.Validation;

[TestClass]
public sealed class RepeatedCollectionValidationTests : ModuleTestBase
{
    [TestMethod]
    public Task TestValidationAsync_RepeatedRequiredAttributesReportParentErrorAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = CreateModule() with
        {
            RequiredTags = null,
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.HasCount(1, result.Errors);
        MSAssert.Contains(
            error => error.Rule == "required"
                && error.PropertyName.Equals(nameof(ValidationPipelineTestModule.RequiredTags), StringComparison.Ordinal),
            result.Errors);
    });

    [TestMethod]
    public Task TestValidationAsync_RepeatedRequiredAttributesReportElementErrorAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        ValidationPipelineTestModule module = CreateModule() with
        {
            RequiredTags = [null],
        };

        IValidationResult<ValidationPipelineTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.HasCount(1, result.Errors);
        MSAssert.Contains(
            error => error.Rule == "required"
                && error.PropertyName.Equals("RequiredTags[0]", StringComparison.Ordinal),
            result.Errors);
    });

    private static ValidationPipelineTestModule CreateModule() => new
    (
        RequiredItems: [],
        OptionalItems: [],
        NullableItems: null,
        InterpolatedValue: "literal",
        DeferredValue: "literal",
        Tags: null
    );
}
