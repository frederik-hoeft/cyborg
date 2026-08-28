using ConsoleAppFramework;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Services.ModuleDescriptors;
using Cyborg.Core.Runtime.Services.Validation;

namespace Cyborg.Cli.Debugging.Commands;

internal sealed class DebugInspectionCommands(IValidationResult<IModule> validationResult, IDebugReplIo io, IModuleSerializationService moduleSerializationService)
{
    /// <summary>Print the prepared state of the current module and any validation errors.</summary>
    [Command("inspect|i")]
    public async Task InspectAsync(CancellationToken cancellationToken)
    {
        IModuleDescriptor descriptor = validationResult.Module.GetDescriptor();

        string inspection = await moduleSerializationService.ToTextAsync(descriptor, cancellationToken);
        await io.WriteLineAsync(inspection, OutputKind.Text, cancellationToken);
        await DebugValidationOutput.WriteErrorsAsync(io, validationResult.Errors, cancellationToken);
    }
}
