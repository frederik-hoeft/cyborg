using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cyborg.Core.TestAdapter;

/// <summary>
/// Encapsulates the module runtime environment, service provider, and deserialization context
/// needed to load, validate, and execute a module within a test. Implements <see cref="IAsyncDisposable"/>
/// to ensure proper cleanup of the scoped service provider.
/// </summary>
/// <remarks>
/// Each test invocation creates a fresh <see cref="TestModuleRuntimeScope"/> so that tests are fully
/// isolated. The scope owns the <see cref="ServiceProvider"/> and disposes it at the end of the test.
/// </remarks>
public sealed class TestModuleRuntimeScope : IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;

    /// <summary>
    /// Gets the root module runtime for this test scope.
    /// </summary>
    public RootModuleRuntime Runtime { get; }

    /// <summary>
    /// Gets the global runtime environment where top-level variables are stored.
    /// </summary>
    public GlobalRuntimeEnvironment GlobalEnvironment { get; }

    /// <summary>
    /// Gets the service provider backing this test scope.
    /// </summary>
    public IServiceProvider ServiceProvider => _serviceProvider;

    private TestModuleRuntimeScope(ServiceProvider serviceProvider, RootModuleRuntime runtime, GlobalRuntimeEnvironment globalEnvironment)
    {
        _serviceProvider = serviceProvider;
        Runtime = runtime;
        GlobalEnvironment = globalEnvironment;
    }

    /// <summary>
    /// Creates a new test scope from the given service collection. Builds the service provider,
    /// resolves the global environment and logger factory, and constructs a <see cref="RootModuleRuntime"/>.
    /// </summary>
    /// <param name="services">The fully configured service collection.</param>
    /// <returns>A ready-to-use test scope.</returns>
    internal static TestModuleRuntimeScope Create(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        GlobalRuntimeEnvironment globalEnvironment = serviceProvider.GetRequiredService<GlobalRuntimeEnvironment>();
        ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        RootModuleRuntime runtime = new(globalEnvironment, loggerFactory, serviceProvider);
        return new TestModuleRuntimeScope(serviceProvider, runtime, globalEnvironment);
    }

    /// <summary>
    /// Deserializes a module JSON string into a <see cref="ModuleReference"/> by running it through
    /// the registry-based deserialization pipeline (the same path used in production).
    /// </summary>
    /// <param name="moduleJson">The JSON string representing a module reference (e.g., <c>{ "cyborg.modules.subprocess.v1": { ... } }</c>).</param>
    /// <returns>The deserialized immutable module reference.</returns>
    public ModuleReference DeserializeModule(string moduleJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleJson);
        IJsonLoaderContext loaderContext = _serviceProvider.GetRequiredService<IJsonLoaderContext>();
        ModuleReference moduleReference = JsonSerializer.Deserialize<ModuleReference>(moduleJson, loaderContext.JsonSerializerOptions)
            ?? throw new InvalidOperationException("Deserialization of the module JSON returned null. Verify the JSON is a valid module reference.");
        return moduleReference;
    }

    /// <summary>
    /// Deserializes a module context JSON string into a <see cref="ModuleContext"/> by running it through
    /// the registry-based deserialization pipeline.
    /// </summary>
    /// <param name="moduleContextJson">The JSON string representing a full module context.</param>
    /// <returns>The deserialized module context.</returns>
    public ModuleContext DeserializeModuleContext(string moduleContextJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleContextJson);
        IJsonLoaderContext loaderContext = _serviceProvider.GetRequiredService<IJsonLoaderContext>();
        ModuleContext moduleContext = JsonSerializer.Deserialize<ModuleContext>(moduleContextJson, loaderContext.JsonSerializerOptions)
            ?? throw new InvalidOperationException("Deserialization of the module context JSON returned null. Verify the JSON is a valid module context.");
        return moduleContext;
    }

    /// <summary>
    /// Extracts the concrete module record of type <typeparamref name="TModule"/> from a deserialized
    /// <see cref="ModuleReference"/>.
    /// </summary>
    /// <typeparam name="TModule">The expected concrete module record type.</typeparam>
    /// <param name="moduleReference">The deserialized module reference.</param>
    /// <returns>The typed module record.</returns>
    public static TModule ExtractModule<TModule>(ModuleReference moduleReference) where TModule : ModuleBase, IModule
    {
        ArgumentNullException.ThrowIfNull(moduleReference);
        if (moduleReference.Definition is TModule typedModule)
        {
            return typedModule;
        }
        throw new InvalidOperationException(
            $"Expected module of type '{typeof(TModule).Name}' but the module reference contains '{moduleReference.Definition.GetType().Name}'.");
    }

    /// <summary>
    /// Activates a fresh worker from a loaded module reference using this scope's service provider.
    /// Normal execution should prefer <see cref="ExecuteAsync(ModuleReference,CancellationToken)"/> so the runtime owns activation timing.
    /// </summary>
    public IModuleWorker ActivateWorker(ModuleReference moduleReference)
    {
        ArgumentNullException.ThrowIfNull(moduleReference);
        IModuleWorkerFactory workerFactory = _serviceProvider.GetRequiredService<IModuleWorkerFactory>();
        return workerFactory.CreateWorker(moduleReference, _serviceProvider);
    }

    /// <summary>
    /// Executes a module worker within this scope's runtime and returns the execution result.
    /// </summary>
    /// <param name="worker">The module worker to execute.</param>
    /// <param name="cancellationToken">A cancellation token for the execution.</param>
    /// <returns>The module execution result.</returns>
    public Task<IModuleExecutionResult> ExecuteAsync(IModuleWorker worker, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);
        return Runtime.ExecuteActivatedWorkerAsync(worker, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Executes a loaded module reference. The runtime activates a fresh worker immediately before execution.
    /// </summary>
    public Task<IModuleExecutionResult> ExecuteAsync(ModuleReference moduleReference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleReference);
        return Runtime.ExecuteAsync(moduleReference, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Executes a module context within this scope's runtime and returns the execution result.
    /// </summary>
    /// <param name="moduleContext">The module context to execute.</param>
    /// <param name="cancellationToken">A cancellation token for the execution.</param>
    /// <returns>The module execution result.</returns>
    public Task<IModuleExecutionResult> ExecuteAsync(ModuleContext moduleContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleContext);
        return Runtime.ExecuteAsync(moduleContext, cancellationToken);
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();
}
