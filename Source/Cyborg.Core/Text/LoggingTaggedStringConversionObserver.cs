using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Text;

public sealed partial class LoggingTaggedStringConversionObserver(ILoggerFactory loggerFactory) : ITaggedStringConversionObserver
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("cyborg.core.text.tagged-string");

    public void OnImplicitStringRetrieval(string variableName, TaggedString value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variableName);
        if (!value.HasTags)
        {
            return;
        }

        string tags = string.Join(", ", value.Tags.OrderBy(static tag => tag, StringComparer.Ordinal));
        LogImplicitStringRetrieval(_logger, variableName, tags);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Variable '{VariableName}' was retrieved as string, discarding tags [{Tags}]. Prefer TryResolveVariable(..., out TaggedString).")]
    private static partial void LogImplicitStringRetrieval(ILogger logger, string variableName, string tags);
}
