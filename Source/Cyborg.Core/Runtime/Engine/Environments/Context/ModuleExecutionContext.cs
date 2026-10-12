using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine.Environments.Context;

/// <summary>
/// Bind-site seam that turns a module reference into the contextual frame for its execution view.
/// Additional definition-site facts are composed here.
/// </summary>
internal static class ModuleExecutionContext
{
    public static IEnvironmentContext Create(ModuleReference moduleReference)
    {
        ArgumentNullException.ThrowIfNull(moduleReference);
        return EnvironmentContext.Compose([EnvironmentContext.FromOrigin(moduleReference.Origin)]);
    }
}
