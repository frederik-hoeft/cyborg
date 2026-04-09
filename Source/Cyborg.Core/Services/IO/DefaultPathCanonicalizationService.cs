namespace Cyborg.Core.Services.IO;

public sealed class DefaultPathCanonicalizationService : IPathCanonicalizationService
{
    public string Canonicalize(string path)
    {
        string fullPath = Path.GetFullPath(path);
        return File.ResolveLinkTarget(fullPath, returnFinalTarget: true)?.FullName ?? fullPath;
    }
}
