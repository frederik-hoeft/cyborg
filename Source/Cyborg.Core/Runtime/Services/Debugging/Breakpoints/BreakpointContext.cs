namespace Cyborg.Core.Runtime.Services.Debugging.Breakpoints;

public readonly record struct BreakpointContext(string ModuleId, string? Name, string? Group)
{
    public IEnumerable<string> GetMatchTargets()
    {
        yield return ModuleId;
        if (!string.IsNullOrWhiteSpace(Name))
        {
            yield return Name;
        }
        if (!string.IsNullOrWhiteSpace(Group))
        {
            yield return Group;
        }
    }
}
