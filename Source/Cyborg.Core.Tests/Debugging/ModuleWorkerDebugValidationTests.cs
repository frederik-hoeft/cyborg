using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Validation;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class ModuleWorkerDebugValidationTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ExecuteAsync_InvalidModule_BreaksBeforeValidationIsEnforcedAsync()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        RecordingDebugger debugger = new();
        TestServiceProvider services = new(loggerFactory, debugger);
        ProbeModule module = new() { Name = "invalid-probe" };
        ProbeWorker worker = new(new ProbeWorkerContext(module, services));
        RootModuleRuntime runtime = new(new GlobalRuntimeEnvironment(JsonNamingPolicy.SnakeCaseLower), loggerFactory);

        await Assert.ThrowsAsync<ValidationException>(async () => await ((IModuleWorker)worker).ExecuteAsync(runtime, TestContext.CancellationToken));

        Assert.AreEqual(1, debugger.EvaluationCount);
        Assert.AreEqual(module, debugger.LastModule);
        Assert.HasCount(1, debugger.LastErrors);
        Assert.IsFalse(worker.ExecuteCalled);
    }

    private sealed record ProbeModule : ModuleBase, IModule<ProbeModule>
    {
        public static string ModuleId => "cyborg.tests.debug-validation.v1";

        public ValueTask<IValidationResult<ProbeModule>> ValidateAsync(IModuleRuntime runtime, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ValidationResult.Invalid(this, [new ValidationError(nameof(Name), "test-rule", "The probe is invalid.")]));
        }
    }

    private sealed class ProbeWorker(IWorkerContext<ProbeModule> context) : ModuleWorker<ProbeModule>(context)
    {
        public bool ExecuteCalled { get; private set; }

        protected override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
        {
            ExecuteCalled = true;
            throw new InvalidOperationException("Execution must not run for an invalid module.");
        }
    }

    private sealed record ProbeWorkerContext(ProbeModule Module, IServiceProvider ServiceProvider) : IWorkerContext<ProbeModule>;

    private sealed class RecordingDebugger : IWorkflowDebugger
    {
        public bool IsEnabled => true;

        public int EvaluationCount { get; private set; }

        public IModule? LastModule { get; private set; }

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
            LastModule = validationResult.Module;
            LastErrors = validationResult.Errors;
            return ValueTask.FromResult(DebugResumeAction.Continue);
        }
    }

    private sealed class TestServiceProvider(ILoggerFactory loggerFactory, IWorkflowDebugger debugger) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(ILoggerFactory))
            {
                return loggerFactory;
            }
            if (serviceType == typeof(IWorkflowDebugger))
            {
                return debugger;
            }
            if (serviceType == typeof(IServiceProvider))
            {
                return this;
            }
            return null;
        }
    }
}
