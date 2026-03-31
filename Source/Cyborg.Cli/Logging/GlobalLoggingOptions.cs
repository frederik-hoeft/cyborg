using Cyborg.Core.Aot.Modules.Composition;
using Microsoft.Extensions.Logging;

namespace Cyborg.Cli.Logging;

[GeneratedDecomposition]
internal sealed partial record GlobalLoggingOptions(LogLevel MinimumLevel = LogLevel.Information);
