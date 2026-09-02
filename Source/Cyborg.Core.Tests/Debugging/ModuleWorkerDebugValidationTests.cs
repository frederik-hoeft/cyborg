using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Debugging;
using Cyborg.Core.Runtime.Services.Validation;
using Cyborg.TestModules.Debugging;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class ModuleWorkerDebugValidationTests : CyborgCoreTestBase
{
    [TestMethod]
    public Task Test_ExecuteAsync_InvalidModule_BreaksBeforeValidationIsEnforcedAsync() => TestWithDIAsync(async services =>
    {
        DebugValidationTestModule module = new() { Name = "invalid-probe" };
        ProbeWorker worker = new(CreateWorkerContext(module, services));
        IModuleWorker boxedWorker = worker;
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        await Assert.ThrowsAsync<ValidationException>(async () => await boxedWorker.ExecuteAsync(runtime, TestContext.CancellationToken));

        IWorkflowDebugger boxedDebugger = services.GetRequiredService<IWorkflowDebugger>();
        Assert.IsInstanceOfType<RecordingDebugger>(boxedDebugger);
        RecordingDebugger debugger = (RecordingDebugger)boxedDebugger;

        Assert.AreEqual(1, debugger.EvaluationCount);
        Assert.AreEqual(DebugValidationTestModule.ModuleId, debugger.LastModuleId);
        Assert.HasCount(1, debugger.LastErrors);
        Assert.IsFalse(worker.ExecuteCalled);
    }, static services => services.AddSingleton<IWorkflowDebugger, RecordingDebugger>());

    private sealed class ProbeWorker(IWorkerContext<DebugValidationTestModule> context) : ModuleWorker<DebugValidationTestModule>(context)
    {
        public bool ExecuteCalled { get; private set; }

        protected async override ValueTask<IValidationResult<DebugValidationTestModule>> OnValidationAsync(
            IValidationResult<DebugValidationTestModule> validationResult,
            DebugValidationTestModule originalModule,
            CancellationToken cancellationToken)
        {
            await base.OnValidationAsync(validationResult, originalModule, cancellationToken);

            return ValidationResult.Invalid(validationResult.Module, [new ValidationError(nameof(DebugValidationTestModule.Name), "test-rule", "The probe is invalid.")]);
        }

        protected override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
        {
            ExecuteCalled = true;
            throw new InvalidOperationException("Execution must not run for an invalid module.");
        }
    }

    private sealed class RecordingDebugger : IWorkflowDebugger
    {
        public int EvaluationCount { get; private set; }

        public string? LastModuleId { get; private set; }

        public IReadOnlyList<ValidationError> LastErrors { get; private set; } = [];

        public ValueTask<DebugResumeAction> EvaluatePreExecutionAsync(
            string moduleId,
            IValidationResult<IModule> validationResult,
            IModuleRuntime runtime,
            IServiceProvider services,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EvaluationCount++;
            LastModuleId = moduleId;
            LastErrors = validationResult.Errors;
            return ValueTask.FromResult(DebugResumeAction.Continue);
        }
    }
}
