namespace Cyborg.Core.Modules.Hooks;

public interface IModulePostExecutionHook : IModuleLifecycleHook
{
    ValueTask ExecuteAsync(IModulePostExecutionContext context, CancellationToken cancellationToken);
}
