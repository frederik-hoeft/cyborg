using ConsoleAppFramework;
using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Modules.Descriptors;

namespace Cyborg.Cli.Debugging.Commands;

internal sealed class DebugInspectionCommands(IDebugPauseContext context, IDebugReplIo io, IModuleSerializationService moduleSerializationService)
{
    private readonly IDebugPauseContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly IDebugReplIo _io = io ?? throw new ArgumentNullException(nameof(io));
    private readonly IModuleSerializationService _moduleSerializationService = moduleSerializationService ?? throw new ArgumentNullException(nameof(moduleSerializationService));

    /// <summary>Print the full validated state of the current module.</summary>
    [Command("inspect|i")]
    public async Task InspectAsync(CancellationToken cancellationToken)
    {
        if (_context.Module is not IModuleDescriptor descriptor)
        {
            _io.WriteLine("The current module does not expose a description.", DebugReplOutputKind.Warning);
            return;
        }

        string inspection = await _moduleSerializationService.ToTextAsync(descriptor, cancellationToken).ConfigureAwait(false);
        _io.WriteLine(inspection);
    }
}
