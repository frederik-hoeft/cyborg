using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine.Environments.Context;

/// <summary>
/// Publishes <see cref="ModuleOrigin"/> under the <c>origin</c> contextual path.
/// </summary>
internal sealed class OriginEnvironmentContext(ModuleOrigin origin) : IEnvironmentContext
{
    public const string FilePath = "origin.file";

    public const string DirectoryPath = "origin.directory";

    public bool TryGetValue(string path, [NotNullWhen(true)] out object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.Equals(FilePath, StringComparison.Ordinal))
        {
            value = origin.File;
            return true;
        }
        if (path.Equals(DirectoryPath, StringComparison.Ordinal))
        {
            value = origin.Directory;
            return true;
        }
        value = null;
        return false;
    }
}
