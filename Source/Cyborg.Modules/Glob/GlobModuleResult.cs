using Cyborg.Core.Aot.Modules.Composition;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Glob;

[GeneratedDecomposition]
public sealed partial record GlobModuleResult(IEnumerable<string> Files) : IDecomposable;