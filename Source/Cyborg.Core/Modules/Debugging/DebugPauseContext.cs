using Cyborg.Core.Modules.Descriptors;
using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Core.Modules.Debugging;

internal sealed class DebugPauseContext(
    IModule module,
    string moduleId,
    IModuleRuntime runtime,
    IBreakpointRegistry breakpoints,
    IModuleDescriptionSerializer textSerializer,
    Action requestStep,
    Action detach) : IDebugPauseContext
{
    private readonly IModuleDescriptionSerializer _textSerializer =
        textSerializer ?? throw new ArgumentNullException(nameof(textSerializer));
    private readonly Action _requestStep =
        requestStep ?? throw new ArgumentNullException(nameof(requestStep));
    private readonly Action _detach =
        detach ?? throw new ArgumentNullException(nameof(detach));

    public IModule Module { get; } =
        module ?? throw new ArgumentNullException(nameof(module));

    public string ModuleId { get; } =
        !string.IsNullOrWhiteSpace(moduleId)
            ? moduleId
            : throw new ArgumentException("Module id must not be null or whitespace.", nameof(moduleId));

    public string ModuleIdentity { get; } =
        global::Cyborg.Core.Modules.Debugging.ModuleIdentity.Format(module, moduleId);

    public IModuleRuntime Runtime { get; } =
        runtime ?? throw new ArgumentNullException(nameof(runtime));

    public IBreakpointRegistry Breakpoints { get; } =
        breakpoints ?? throw new ArgumentNullException(nameof(breakpoints));

    public ValueTask<string> InspectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Module is IModuleDescriptor descriptor)
        {
            return ModuleDescription.SerializeAsync(
                descriptor,
                _textSerializer,
                cancellationToken);
        }

        return ValueTask.FromResult(ModuleIdentity);
    }

    public void RequestStep() => _requestStep();

    public void Detach() => _detach();
}
