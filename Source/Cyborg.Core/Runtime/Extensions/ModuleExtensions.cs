namespace Cyborg.Core.Runtime.Extensions;

[SuppressMessage("Design", CA1034, Justification = CA1034_JUSTIFY_EXTENSION_SYNTAX_CSHARP_14)]
public static class ModuleExtensions
{
    extension<T>(T self) where T : IModuleDefinition
    {
        public string ToDisplayString() => self switch
        {
            { Name.Length: > 0 } => $"{T.ModuleId} ({self.Name})",
            { Group.Length: > 0 } => $"{T.ModuleId} ({self.Group})",
            _ => T.ModuleId
        };
    }
}
