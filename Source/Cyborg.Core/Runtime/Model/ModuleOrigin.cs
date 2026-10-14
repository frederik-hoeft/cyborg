namespace Cyborg.Core.Runtime.Model;

/// <summary>
/// Identifies the source file defining a module, independently of where the module executes.
/// </summary>
public sealed record ModuleOrigin
{
    public string File { get; }

    public string Directory { get; }

    public ModuleOrigin(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        File = Path.GetFullPath(file);
        Directory = Path.GetDirectoryName(File) ?? throw new ArgumentException("Module origin must identify a file within a directory.", nameof(file));
    }
}
