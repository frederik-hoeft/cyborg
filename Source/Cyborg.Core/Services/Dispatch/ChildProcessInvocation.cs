using Cyborg.Core.Text;
using System.Diagnostics;

namespace Cyborg.Core.Services.Dispatch;

/// <summary>
/// Describes a child-process invocation while preserving metadata on textual values until the
/// process execution boundary.
/// </summary>
public sealed class ChildProcessInvocation
{
    public string FileName { get; }

    public List<TaggedString> ArgumentList { get; } = [];

    public Dictionary<string, TaggedString> Environment { get; } = new(StringComparer.Ordinal);

    public string? WorkingDirectory { get; set; }

    public bool RedirectStandardOutput { get; set; }

    public bool RedirectStandardError { get; set; }

    public ChildProcessInvocation(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        FileName = fileName;
    }

    public ChildProcessInvocation(string fileName, IEnumerable<TaggedString> arguments) : this(fileName)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentList.AddRange(arguments);
    }

    public ChildProcessInvocation(string fileName, IEnumerable<string> arguments) : this(fileName)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentList.AddRange(arguments.Select(static argument => new TaggedString(argument)));
    }

    /// <summary>
    /// Materializes the raw process-start structure. This is the intentional metadata-loss boundary
    /// immediately before execution.
    /// </summary>
    public ProcessStartInfo CreateProcessStartInfo()
    {
        ProcessStartInfo startInfo = new(FileName)
        {
            RedirectStandardOutput = RedirectStandardOutput,
            RedirectStandardError = RedirectStandardError,
        };

        if (!string.IsNullOrEmpty(WorkingDirectory))
        {
            startInfo.WorkingDirectory = WorkingDirectory;
        }

        foreach (TaggedString argument in ArgumentList)
        {
            startInfo.ArgumentList.Add(argument.Value);
        }

        foreach ((string key, TaggedString value) in Environment)
        {
            startInfo.Environment[key] = value.Value;
        }

        return startInfo;
    }
}
