using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Logging;

/// <summary>
/// Represents a contributor that configures the logging pipeline.
/// Implementations are resolved via Jab's <see cref="IEnumerable{T}"/> support
/// and applied when constructing the singleton <see cref="ILoggerFactory"/>.
/// </summary>
public interface ILoggingConfigurator
{
    void Configure(ILoggingBuilder builder);
}
