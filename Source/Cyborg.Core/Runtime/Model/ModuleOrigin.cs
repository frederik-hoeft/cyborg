namespace Cyborg.Core.Runtime.Model;

/// <summary>
/// Definition-site location of a module configuration file.
/// Both paths are absolute. <see cref="Directory"/> has no trailing separator except at a filesystem root.
/// </summary>
public sealed record ModuleOrigin
{
    /// <summary>Absolute, symlink-resolved path of the configuration file.</summary>
    public string File { get; }

    /// <summary>Absolute directory containing <see cref="File"/>.</summary>
    public string Directory { get; }

    private ModuleOrigin(string file, string directory)
    {
        File = file;
        Directory = directory;
    }

    /// <summary>
    /// Creates an origin from a configuration path. Relative paths are made absolute against the current directory.
    /// The caller should pass the canonical path when one is already known.
    /// </summary>
    public static ModuleOrigin FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new ArgumentException($"Module origin path '{path}' does not have a directory.", nameof(path));
        }
        return new ModuleOrigin(fullPath, directory);
    }
}
