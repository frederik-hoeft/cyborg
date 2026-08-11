namespace Cyborg.Core.Modules.Debugging;

[SuppressMessage("Design", CA1034, Justification = CA1034_JUSTIFY_EXTENSION_SYNTAX_CSHARP_14)]
public static class DebugPauseContextExtensions
{
    extension(IDebugPauseContext context)
    {
        public string GetModuleIdentity() => ModuleIdentity.Format(context.ModuleId, context.ValidationResult.Module);
    }
}
