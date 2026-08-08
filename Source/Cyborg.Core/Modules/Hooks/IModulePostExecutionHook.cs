namespace Cyborg.Core.Modules.Hooks;

public interface IModulePostExecutionHook : IModuleExecutionHook
{
    ValueTask ExecuteAsync(IModulePostExecutionContext context, CancellationToken cancellationToken);
}
