using Cyborg.Core.Configuration;
using Cyborg.Core.Configuration.Builders;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Services.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.TestAdapter;

/// <summary>
/// Base class for per-module unit tests using MSTest. Provides a façade of higher-order function (HOF) methods
/// that abstract away module loading, DI container construction, and runtime initialization, allowing derived
/// test classes to focus on the module under test.
/// </summary>
/// <remarks>
/// <para>
/// <b>Service configuration.</b> The default service collection mirrors the production Jab DI graph via
/// reflection-based discovery (see <see cref="TestServiceConfiguration"/>). Derived classes can customize
/// services at two levels:
/// </para>
/// <list type="bullet">
///   <item><b>Per-test-class</b> — Override <see cref="ConfigureServices"/> to add or replace registrations
///   that apply to every test in the class.</item>
///   <item><b>Per-test-case</b> — Pass an optional <c>configureServices</c> lambda to any HOF method to apply
///   registrations that are scoped to a single test invocation.</item>
/// </list>
/// <para>
/// <b>Module JSON source.</b> Each HOF method accepts an optional <c>moduleJson</c> parameter. When omitted,
/// the framework falls back to <see cref="GetDefaultModuleJsonAsync"/>, which derived classes can override to
/// load JSON from an embedded resource or file. HOF-provided JSON always takes precedence.
/// </para>
/// </remarks>
public abstract class CyborgTestBase
{
    /// <summary>
    /// Gets or sets the MSTest <see cref="TestContext"/> for the current test run.
    /// Automatically injected by the MSTest framework.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    #region Service Configuration Extension Points

    /// <summary>
    /// Override in a derived class to customize the service collection for every test in this class.
    /// </summary>
    /// <param name="services">The mutable service collection to configure.</param>
    /// <param name="jabServiceDiscovery">The Jab service discovery instance.</param>
    protected virtual void ConfigureServices(IServiceCollection services, IJabServiceDiscovery jabServiceDiscovery)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(jabServiceDiscovery);

        // Register all production Jab modules via reflection
        jabServiceDiscovery.RegisterFromModule<ICyborgCoreServices>(services);
        services.AddSingleton(TestContext);
        services.AddDefaultTestServices();
    }

    protected virtual void BuildConfiguration(IConfigurationBuilder configuration)
    {
    }

    /// <summary>
    /// Provides a default module JSON string for tests that do not supply one explicitly.
    /// Override in a derived class to load JSON from a file or embedded resource.
    /// </summary>
    /// <returns>
    /// A JSON string representing a module reference. Returns <see langword="null"/> by default,
    /// meaning tests must provide their own JSON unless this method is overridden.
    /// </returns>
    protected virtual ValueTask<string?> GetDefaultModuleJsonAsync() => new(result: null);

    #endregion

    protected static IWorkerContext<TModule> CreateWorkerContext<TModule>(TModule module, IServiceProvider serviceProvider) where TModule : ModuleBase, IModule<TModule>
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        return new DefaultWorkerContext<TModule>(module, serviceProvider);
    }

    protected Task<IModuleExecutionResult> ExecuteWorkerAsync(IModuleWorker worker, IModuleRuntime runtime) =>
        ExecuteWorkerAsync(worker, runtime, TestContext.CancellationToken);

    protected Task<IModuleExecutionResult> ExecuteWorkerAsync(
        IModuleWorker worker,
        IModuleRuntime runtime,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(runtime);
        if (runtime is not IModuleExecutionRuntime executionRuntime)
        {
            throw new ArgumentException("Runtime does not expose Cyborg's internal module-execution capabilities.", nameof(runtime));
        }
        IRuntimeEnvironment environment = runtime.PrepareEnvironment(new ModuleEnvironment { Scope = EnvironmentScope.Global });
        return executionRuntime.ExecuteActivatedWorkerAsync(worker, environment, cancellationToken);
    }

    #region DI-based Testing

    protected async Task TestWithDIAsync(Func<IServiceProvider, Task> assertion, Action<IServiceCollection>? configureServices = null, Action<IConfigurationBuilder>? buildConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        await using TestModuleRuntimeScope scope = await CreateScopeAsync(configureServices, buildConfiguration);
        await assertion(scope.ServiceProvider);
    }

    protected Task TestWithDIAsync(Action<IServiceProvider> assertion, Action<IServiceCollection>? configureServices = null, Action<IConfigurationBuilder>? buildConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        return TestWithDIAsync(serviceProvider =>
            {
                assertion(serviceProvider);
                return Task.CompletedTask;
            },
            configureServices,
            buildConfiguration);
    }

    #endregion

    #region HOF: Module Deserialization Testing

    /// <summary>
    /// Deserializes the provided (or default) module JSON and invokes the test body with the resulting
    /// typed module record, enabling assertions on deserialization correctness and polymorphic dispatch.
    /// </summary>
    /// <typeparam name="TModule">The expected concrete module record type.</typeparam>
    /// <param name="moduleJson">Optional module JSON. Falls back to <see cref="GetDefaultModuleJsonAsync"/> when <see langword="null"/>.</param>
    /// <param name="assertion">The async test body receiving the deserialized module record.</param>
    /// <param name="configureServices">Optional per-test-case service configuration.</param>
    /// <param name="buildConfiguration">Optional per-test-case configuration builder.</param>
    protected async Task TestDeserializationAsync<TModule>(string? moduleJson, Func<TModule, Task> assertion, Action<IServiceCollection>? configureServices = null, Action<IConfigurationBuilder>? buildConfiguration = null)
        where TModule : ModuleBase, IModule
    {
        ArgumentNullException.ThrowIfNull(assertion);
        string resolvedJson = await ResolveModuleJsonAsync(moduleJson);
        await using TestModuleRuntimeScope scope = await CreateScopeAsync(configureServices, buildConfiguration);
        ModuleReference moduleReference = scope.DeserializeModule(resolvedJson);
        TModule module = TestModuleRuntimeScope.ExtractModule<TModule>(moduleReference);
        await assertion(module);
    }

    /// <summary>
    /// Overload of <see cref="TestDeserializationAsync{TModule}(string?,Func{TModule,Task},Action{IServiceCollection}?,Action{IConfigurationBuilder}?)"/>
    /// with a synchronous assertion body for simpler test cases that don't need async assertions.
    /// </summary>
    protected Task TestDeserializationAsync<TModule>(string? moduleJson, Action<TModule> assertion, Action<IServiceCollection>? configureServices = null, Action<IConfigurationBuilder>? buildConfiguration = null)
        where TModule : ModuleBase, IModule
    {
        ArgumentNullException.ThrowIfNull(assertion);
        return TestDeserializationAsync<TModule>(
            moduleJson,
            module =>
            {
                assertion(module);
                return Task.CompletedTask;
            },
            configureServices,
            buildConfiguration);
    }

    /// <summary>
    /// Deserializes a full module context JSON and invokes the test body with the resulting
    /// <see cref="ModuleContext"/>, enabling assertions on environment, configuration, and requires properties.
    /// </summary>
    /// <param name="moduleContextJson">Optional module context JSON. Falls back to <see cref="GetDefaultModuleJsonAsync"/> when <see langword="null"/>.</param>
    /// <param name="assertion">The async test body receiving the deserialized module context.</param>
    /// <param name="configureServices">Optional per-test-case service configuration.</param>
    /// <param name="buildConfiguration">Optional per-test-case configuration builder.</param>
    protected async Task TestContextDeserializationAsync(
        string? moduleContextJson,
        Func<ModuleContext, Task> assertion,
        Action<IServiceCollection>? configureServices = null,
        Action<IConfigurationBuilder>? buildConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        string resolvedJson = await ResolveModuleJsonAsync(moduleContextJson);
        await using TestModuleRuntimeScope scope = await CreateScopeAsync(configureServices, buildConfiguration);
        ModuleContext moduleContext = scope.DeserializeModuleContext(resolvedJson);
        await assertion(moduleContext);
    }

    /// <summary>
    /// Overload of <see cref="TestContextDeserializationAsync(string?,Func{ModuleContext,Task},Action{IServiceCollection}?,Action{IConfigurationBuilder}?)"/>
    /// with a synchronous assertion body for simpler test cases that don't need async assertions.
    /// </summary>
    protected Task TestContextDeserializationAsync(
        string? moduleContextJson,
        Action<ModuleContext> assertion,
        Action<IServiceCollection>? configureServices = null,
        Action<IConfigurationBuilder>? buildConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        return TestContextDeserializationAsync(
            moduleContextJson,
            context =>
            {
                assertion(context);
                return Task.CompletedTask;
            },
            configureServices,
            buildConfiguration);
    }

    #endregion

    #region HOF: Validation Testing

    /// <summary>
    /// Deserializes the module JSON, runs the full generated pipeline (defaults, overrides, defaults, interpolation, constraints),
    /// and invokes the test body with the validation result. Use this to verify that validation attributes
    /// are enforced correctly and that default values are applied as expected.
    /// </summary>
    /// <typeparam name="TModule">The expected concrete module record type (must implement <see cref="IModule{TSelf}"/>).</typeparam>
    /// <param name="moduleJson">Optional module JSON. Falls back to <see cref="GetDefaultModuleJsonAsync"/> when <see langword="null"/>.</param>
    /// <param name="assertion">The async test body receiving the validation result.</param>
    /// <param name="configureServices">Optional per-test-case service configuration.</param>
    protected async Task TestValidationAsync<TModule>(
        string? moduleJson,
        Func<IValidationResult<TModule>, Task> assertion,
        Action<IServiceCollection>? configureServices = null,
        Action<IConfigurationBuilder>? buildConfiguration = null)
        where TModule : ModuleBase, IModule<TModule>
    {
        ArgumentNullException.ThrowIfNull(assertion);
        string resolvedJson = await ResolveModuleJsonAsync(moduleJson);
        await using TestModuleRuntimeScope scope = await CreateScopeAsync(configureServices, buildConfiguration);
        ModuleReference moduleReference = scope.DeserializeModule(resolvedJson);
        TModule module = TestModuleRuntimeScope.ExtractModule<TModule>(moduleReference);
        IValidationResult<TModule> validationResult = await module.ValidateAsync(scope.Runtime, scope.ServiceProvider, TestContext.CancellationToken);
        await assertion(validationResult);
    }

    /// <summary>
    /// Overload of <see cref="TestValidationAsync{TModule}(string?,Func{IValidationResult{TModule},Task},Action{IServiceCollection}?,Action{IConfigurationBuilder}?)"/>
    /// with a synchronous assertion body for simpler test cases that don't need async assertions.
    /// </summary>
    protected Task TestValidationAsync<TModule>(
        string? moduleJson,
        Action<IValidationResult<TModule>> assertion,
        Action<IServiceCollection>? configureServices = null,
        Action<IConfigurationBuilder>? buildConfiguration = null)
        where TModule : ModuleBase, IModule<TModule>
    {
        ArgumentNullException.ThrowIfNull(assertion);
        return TestValidationAsync<TModule>(
            moduleJson,
            result =>
            {
                assertion(result);
                return Task.CompletedTask;
            },
            configureServices,
            buildConfiguration);
    }

    /// <summary>
    /// Deserializes the module JSON, runs the full validation pipeline, and invokes the test body with
    /// the validated module record and the runtime environment. Throws if validation fails.
    /// Use this when the test requires a fully validated module instance.
    /// </summary>
    /// <typeparam name="TModule">The expected concrete module record type.</typeparam>
    /// <param name="moduleJson">Optional module JSON. Falls back to <see cref="GetDefaultModuleJsonAsync"/> when <see langword="null"/>.</param>
    /// <param name="assertion">The async test body receiving the validated module and runtime scope.</param>
    /// <param name="configureServices">Optional per-test-case service configuration.</param>
    /// <param name="buildConfiguration">Optional per-test-case configuration builder setup.</param>
    protected async Task TestValidatedModuleAsync<TModule>(
        string? moduleJson,
        Func<TModule, TestModuleRuntimeScope, Task> assertion,
        Action<IServiceCollection>? configureServices = null,
        Action<IConfigurationBuilder>? buildConfiguration = null)
        where TModule : ModuleBase, IModule<TModule>
    {
        ArgumentNullException.ThrowIfNull(assertion);
        string resolvedJson = await ResolveModuleJsonAsync(moduleJson);
        await using TestModuleRuntimeScope scope = await CreateScopeAsync(configureServices, buildConfiguration);
        ModuleReference moduleReference = scope.DeserializeModule(resolvedJson);
        TModule module = TestModuleRuntimeScope.ExtractModule<TModule>(moduleReference);
        IValidationResult<TModule> validationResult = await module.ValidateAsync(scope.Runtime, scope.ServiceProvider, TestContext.CancellationToken);
        validationResult.EnsureValid();
        await assertion(validationResult.Module, scope);
    }

    #endregion

    #region HOF: Override Testing

    /// <summary>
    /// Deserializes the module JSON, applies the given environment variables to the global environment,
    /// then runs the validation pipeline (which includes override resolution) and invokes the test body
    /// with the resulting module. Use this to verify that runtime overrides (variable injection, default
    /// value substitution) are applied correctly.
    /// </summary>
    /// <typeparam name="TModule">The expected concrete module record type.</typeparam>
    /// <param name="moduleJson">Optional module JSON. Falls back to <see cref="GetDefaultModuleJsonAsync"/> when <see langword="null"/>.</param>
    /// <param name="environmentSetup">Callback to configure environment variables before validation.</param>
    /// <param name="assertion">The async test body receiving the fully resolved module.</param>
    /// <param name="configureServices">Optional per-test-case service configuration.</param>
    /// <param name="buildConfiguration">Optional per-test-case configuration builder setup.</param>
    protected async Task TestOverridesAsync<TModule>(
        string? moduleJson,
        Action<IRuntimeEnvironment> environmentSetup,
        Func<TModule, Task> assertion,
        Action<IServiceCollection>? configureServices = null,
        Action<IConfigurationBuilder>? buildConfiguration = null)
        where TModule : ModuleBase, IModule<TModule>
    {
        ArgumentNullException.ThrowIfNull(environmentSetup);
        ArgumentNullException.ThrowIfNull(assertion);
        string resolvedJson = await ResolveModuleJsonAsync(moduleJson);
        await using TestModuleRuntimeScope scope = await CreateScopeAsync(configureServices, buildConfiguration);
        environmentSetup(scope.GlobalEnvironment);
        ModuleReference moduleReference = scope.DeserializeModule(resolvedJson);
        TModule module = TestModuleRuntimeScope.ExtractModule<TModule>(moduleReference);
        IValidationResult<TModule> validationResult = await module.ValidateAsync(scope.Runtime, scope.ServiceProvider, TestContext.CancellationToken);
        validationResult.EnsureValid();
        await assertion(validationResult.Module);
    }

    /// <summary>
    /// Overload of <see cref="TestOverridesAsync{TModule}(string?,Action{IRuntimeEnvironment},Func{TModule,Task},Action{IServiceCollection}?),Action{IConfigurationBuilder}?)"/>
    /// with a synchronous assertion body for simpler test cases that don't need async assertions.
    /// </summary>
    protected Task TestOverridesAsync<TModule>(
        string? moduleJson,
        Action<IRuntimeEnvironment> environmentSetup,
        Action<TModule> assertion,
        Action<IServiceCollection>? configureServices = null,
        Action<IConfigurationBuilder>? buildConfiguration = null)
        where TModule : ModuleBase, IModule<TModule>
    {
        ArgumentNullException.ThrowIfNull(assertion);
        return TestOverridesAsync<TModule>(
            moduleJson,
            environmentSetup,
            module =>
            {
                assertion(module);
                return Task.CompletedTask;
            },
            configureServices,
            buildConfiguration);
    }

    #endregion

    #region HOF: Module Execution Testing

    /// <summary>
    /// Deserializes and executes the module, then invokes the test body with the typed module worker and
    /// the execution result. This is the primary HOF for testing module worker correctness, result
    /// publishing, artifact paths, and exit status.
    /// </summary>
    /// <typeparam name="TModule">The expected concrete module record type.</typeparam>
    /// <typeparam name="TWorker">The expected module worker type.</typeparam>
    /// <param name="moduleJson">Optional module JSON. Falls back to <see cref="GetDefaultModuleJsonAsync"/> when <see langword="null"/>.</param>
    /// <param name="assertion">The async test body receiving the module, worker, and execution result.</param>
    /// <param name="environmentSetup">Optional callback to configure environment variables before execution.</param>
    /// <param name="configureServices">Optional per-test-case service configuration.</param>
    /// <param name="buildConfiguration">Optional per-test-case configuration builder setup.</param>
    protected async Task TestModuleAsync<TModule, TWorker>(
        string? moduleJson,
        Func<TModule, TWorker, IModuleExecutionResult, Task> assertion,
        Action<IRuntimeEnvironment>? environmentSetup = null,
        Action<IServiceCollection>? configureServices = null,
        Action<IConfigurationBuilder>? buildConfiguration = null)
        where TModule : ModuleBase, IModule
        where TWorker : class, IModuleWorker
    {
        ArgumentNullException.ThrowIfNull(assertion);
        string resolvedJson = await ResolveModuleJsonAsync(moduleJson);
        await using TestModuleRuntimeScope scope = await CreateScopeAsync(configureServices, buildConfiguration);
        environmentSetup?.Invoke(scope.GlobalEnvironment);
        ModuleReference moduleReference = scope.DeserializeModule(resolvedJson);
        TModule module = TestModuleRuntimeScope.ExtractModule<TModule>(moduleReference);
        IModuleWorker worker = scope.ActivateWorker(moduleReference);
        IModuleExecutionResult result = await scope.ExecuteAsync(worker, TestContext.CancellationToken);
        if (worker is not TWorker typedWorker)
        {
            throw new InvalidOperationException(
                $"Expected worker of type '{typeof(TWorker).Name}' but got '{worker.GetType().Name}'.");
        }
        await assertion(module, typedWorker, result);
    }

    /// <summary>
    /// Overload of <see cref="TestModuleAsync{TModule,TWorker}(string?,Func{TModule,TWorker,IModuleExecutionResult,Task},Action{IRuntimeEnvironment}?,Action{IServiceCollection}?,Action{IConfigurationBuilder}?)"/>
    /// with a synchronous assertion body for simpler test cases that don't need async assertions.
    /// </summary>
    protected Task TestModuleAsync<TModule, TWorker>(
        string? moduleJson,
        Action<TModule, TWorker, IModuleExecutionResult> assertion,
        Action<IRuntimeEnvironment>? environmentSetup = null,
        Action<IServiceCollection>? configureServices = null,
        Action<IConfigurationBuilder>? buildConfiguration = null)
        where TModule : ModuleBase, IModule
        where TWorker : class, IModuleWorker
    {
        return TestModuleAsync<TModule, TWorker>(
            moduleJson,
            (module, worker, result) =>
            {
                assertion(module, worker, result);
                return Task.CompletedTask;
            },
            environmentSetup,
            configureServices,
            buildConfiguration);
    }

    #endregion

    #region HOF: Execution Result Testing (Simplified)

    /// <summary>
    /// Deserializes and executes the module, then invokes the test body with just the execution result.
    /// Use this when you only need to verify the exit status, artifacts, or error behavior without
    /// inspecting the worker or module record.
    /// </summary>
    /// <param name="moduleJson">Optional module JSON. Falls back to <see cref="GetDefaultModuleJsonAsync"/> when <see langword="null"/>.</param>
    /// <param name="assertion">The async test body receiving the execution result.</param>
    /// <param name="environmentSetup">Optional callback to configure environment variables before execution.</param>
    /// <param name="configureServices">Optional per-test-case service configuration.</param>
    /// <param name="buildConfiguration">Optional per-test-case configuration builder setup.</param>
    protected async Task TestExecutionAsync(
        string? moduleJson,
        Func<IModuleExecutionResult, Task> assertion,
        Action<IRuntimeEnvironment>? environmentSetup = null,
        Action<IServiceCollection>? configureServices = null,
        Action<IConfigurationBuilder>? buildConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        string resolvedJson = await ResolveModuleJsonAsync(moduleJson);
        await using TestModuleRuntimeScope scope = await CreateScopeAsync(configureServices, buildConfiguration);
        environmentSetup?.Invoke(scope.GlobalEnvironment);
        ModuleReference moduleReference = scope.DeserializeModule(resolvedJson);
        IModuleExecutionResult result = await scope.ExecuteAsync(moduleReference, TestContext.CancellationToken);
        await assertion(result);
    }

    /// <summary>
    /// Overload of <see cref="TestExecutionAsync(string?,Func{IModuleExecutionResult,Task},Action{IRuntimeEnvironment}?,Action{IServiceCollection}?,Action{IConfigurationBuilder}?)"/>
    /// with a synchronous assertion body for simpler test cases that don't need async assertions.
    /// </summary>
    protected Task TestExecutionAsync(
        string? moduleJson,
        Action<IModuleExecutionResult> assertion,
        Action<IRuntimeEnvironment>? environmentSetup = null,
        Action<IServiceCollection>? configureServices = null,
        Action<IConfigurationBuilder>? buildConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        return TestExecutionAsync(moduleJson, result =>
        {
            assertion(result);
            return Task.CompletedTask;
        }, environmentSetup, configureServices, buildConfiguration);
    }

    /// <summary>
    /// Deserializes and executes the module, asserting that the execution throws an exception of type
    /// <typeparamref name="TException"/>. Use this for verifying exception handling and error reporting
    /// (e.g., validation failures, missing required fields).
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="moduleJson">Optional module JSON. Falls back to <see cref="GetDefaultModuleJsonAsync"/> when <see langword="null"/>.</param>
    /// <param name="assertion">Optional async callback to inspect the thrown exception.</param>
    /// <param name="environmentSetup">Optional callback to configure environment variables before execution.</param>
    /// <param name="configureServices">Optional per-test-case service configuration.</param>
    /// <param name="buildConfiguration">Optional per-test-case configuration builder setup.</param>
    protected async Task TestExecutionThrowsAsync<TException>(
        string? moduleJson,
        Func<TException, Task>? assertion = null,
        Action<IRuntimeEnvironment>? environmentSetup = null,
        Action<IServiceCollection>? configureServices = null,
        Action<IConfigurationBuilder>? buildConfiguration = null)
        where TException : Exception
    {
        string resolvedJson = await ResolveModuleJsonAsync(moduleJson);
        await using TestModuleRuntimeScope scope = await CreateScopeAsync(configureServices, buildConfiguration);
        environmentSetup?.Invoke(scope.GlobalEnvironment);
        ModuleReference moduleReference = scope.DeserializeModule(resolvedJson);
        TException exception = await Assert.ThrowsExactlyAsync<TException>(
            () => scope.ExecuteAsync(moduleReference, TestContext.CancellationToken));
        if (assertion is not null)
        {
            await assertion(exception);
        }
    }

    /// <summary>
    /// Overload of <see cref="TestExecutionThrowsAsync{TException}(string?,Func{TException,Task}?,Action{IRuntimeEnvironment}?,Action{IServiceCollection}?,Action{IConfigurationBuilder}?)"/>
    /// with a synchronous assertion body for simpler test cases that don't need async assertions.
    /// </summary>
    protected Task TestExecutionThrowsAsync<TException>(
        string? moduleJson,
        Action<TException>? assertion,
        Action<IRuntimeEnvironment>? environmentSetup = null,
        Action<IServiceCollection>? configureServices = null,
        Action<IConfigurationBuilder>? buildConfiguration = null)
        where TException : Exception
    {
        return TestExecutionThrowsAsync<TException>(
            moduleJson,
            assertion is null ? null : ex =>
            {
                assertion(ex);
                return Task.CompletedTask;
            },
            environmentSetup,
            configureServices,
            buildConfiguration);
    }

    #endregion

    #region HOF: Full Module Context Execution Testing

    /// <summary>
    /// Deserializes a full module context JSON (including environment, configuration, and requires) and
    /// executes it within the runtime. Invokes the test body with the execution result and the runtime
    /// scope, allowing inspection of the execution result and the runtime environment after execution.
    /// </summary>
    /// <param name="moduleContextJson">Optional module context JSON. Falls back to <see cref="GetDefaultModuleJsonAsync"/> when <see langword="null"/>.</param>
    /// <param name="assertion">The async test body receiving the execution result and the runtime scope.</param>
    /// <param name="environmentSetup">Optional callback to configure environment variables before execution.</param>
    /// <param name="configureServices">Optional per-test-case service configuration.</param>
    /// <param name="buildConfiguration">Optional per-test-case configuration builder setup.</param>
    protected async Task TestModuleContextAsync(
        string? moduleContextJson,
        Func<IModuleExecutionResult, TestModuleRuntimeScope, Task> assertion,
        Action<IRuntimeEnvironment>? environmentSetup = null,
        Action<IServiceCollection>? configureServices = null,
        Action<IConfigurationBuilder>? buildConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        string resolvedJson = await ResolveModuleJsonAsync(moduleContextJson);
        await using TestModuleRuntimeScope scope = await CreateScopeAsync(configureServices, buildConfiguration);
        environmentSetup?.Invoke(scope.GlobalEnvironment);
        ModuleContext moduleContext = scope.DeserializeModuleContext(resolvedJson);
        IModuleExecutionResult result = await scope.ExecuteAsync(moduleContext, TestContext.CancellationToken);
        await assertion(result, scope);
    }

    #endregion

    #region Private Helpers

    private async ValueTask<TestModuleRuntimeScope> CreateScopeAsync(Action<IServiceCollection>? perTestConfigureServices, Action<IConfigurationBuilder>? perTestConfigureConfiguration)
    {
        IServiceCollection services = TestServiceConfiguration.CreateDefaultServices();
        ConfigureServices(services, new JabServiceDiscovery());
        perTestConfigureServices?.Invoke(services);
        TestModuleRuntimeScope testScope = TestModuleRuntimeScope.Create(services);
        IConfigurationBuilder configurationBuilder = testScope.ServiceProvider.GetRequiredService<IConfigurationBuilder>();
        BuildConfiguration(configurationBuilder);
        perTestConfigureConfiguration?.Invoke(configurationBuilder);
        IConfiguration configuration = testScope.ServiceProvider.GetRequiredService<IConfiguration>();
        await configurationBuilder.ApplyToAsync(configuration, TestContext.CancellationToken);
        return testScope;
    }

    private async Task<string> ResolveModuleJsonAsync(string? explicitJson)
    {
        if (!string.IsNullOrWhiteSpace(explicitJson))
        {
            return explicitJson;
        }
        string? defaultJson = await GetDefaultModuleJsonAsync();
        if (string.IsNullOrWhiteSpace(defaultJson))
        {
            throw new InvalidOperationException(
                "No module JSON was provided and GetDefaultModuleJsonAsync() returned null or empty. " +
                "Either pass module JSON to the HOF method or override GetDefaultModuleJsonAsync() in the test class.");
        }
        return defaultJson;
    }

    #endregion
}
