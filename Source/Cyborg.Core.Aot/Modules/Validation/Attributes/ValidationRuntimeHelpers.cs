namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

internal static class ValidationRuntimeHelpers
{
    public static bool IsValidFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName is "." or "..")
        {
            return false;
        }

        return fileName.IndexOfAny(Path.GetInvalidFileNameChars()) == -1;
    }

    public static bool IsNormalizedPath(string path)
    {
        ReadOnlySpan<char> pathSpan = path.AsSpan();
        for (int start = 0; start < pathSpan.Length;)
        {
            int next = pathSpan[start..].IndexOfAny(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (next < 0)
            {
                return pathSpan[start..] is not "." and not "..";
            }
            next = start + next;
            if (next == start && start != 0 || pathSpan[start..next] is "." or "..")
            {
                return false;
            }
            start = next + 1;
        }

        return true;
    }
}
