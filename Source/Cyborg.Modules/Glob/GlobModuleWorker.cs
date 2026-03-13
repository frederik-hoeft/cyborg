using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Cyborg.Modules.Glob;

public sealed class GlobModuleWorker(IWorkerContext<GlobModule> context) : ModuleWorker<GlobModule>(context)
{
    protected override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        Regex regex = new(Module.Pattern, RegexOptions.IgnoreCase);
        DirectoryInfo rootInfo = new(Module.Root);
        IEnumerable<string> files = rootInfo.EnumerateFiles("*", Module.Recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Select(f => f.FullName)
            .Where(f => regex.IsMatch(f));
        GlobModuleResult result = new(files);
        return Task.FromResult(runtime.Exit(Success(result)));
    }
}