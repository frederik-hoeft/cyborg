namespace Cyborg.Core.Runtime.Hooks;

public interface IModulePostExecutionHook : IModuleLifecycleHook
{
    ValueTask ExecuteAsync(IModulePostExecutionContext context, CancellationToken cancellationToken);
}
