using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Core.Modules.Runtime;

internal static partial class ModuleRuntimeLog
{
    // ── Dispatch ─────────────────────────────────────────────────────────────

    [ZLoggerMessage(LogLevel.Debug, "Dispatching module '{moduleId}' in environment '{environment}'")]
    public static partial void LogModuleDispatched(this ILogger logger, string moduleId, string environment);

    [ZLoggerMessage(LogLevel.Debug, "Module '{moduleId}' completed with status '{status}' in environment '{environment}'")]
    public static partial void LogModuleCompleted(this ILogger logger, string moduleId, string status, string environment);

    [ZLoggerMessage(LogLevel.Warning, "Module '{moduleId}' in environment '{environment}' completed with non-success status: {status}")]
    public static partial void LogModuleExecutionFailed(this ILogger logger, string moduleId, string status, string environment);

    [ZLoggerMessage(LogLevel.Warning, "Module '{moduleId}' in environment '{environment}' was canceled")]
    public static partial void LogModuleCanceled(this ILogger logger, string moduleId, string environment);

    [ZLoggerMessage(LogLevel.Error, "Module '{moduleId}' in environment '{environment}' threw an unhandled exception")]
    public static partial void LogModuleUnhandledException(this ILogger logger, string moduleId, string environment, Exception exception);

    // ── Module context execution ──────────────────────────────────────────────

    [ZLoggerMessage(LogLevel.Debug, "Running configuration module '{configModuleId}' before main module '{mainModuleId}'")]
    public static partial void LogConfigurationModuleRunning(this ILogger logger, string configModuleId, string mainModuleId);

    [ZLoggerMessage(LogLevel.Debug, "Resolving {count} template argument(s) for module '{moduleId}' (namespace: '{argumentNamespace}')")]
    public static partial void LogTemplateArgumentsResolving(this ILogger logger, int count, string moduleId, string argumentNamespace);

    [ZLoggerMessage(LogLevel.Error, "Template argument resolution failed for module '{moduleId}': {errors}")]
    public static partial void LogTemplateArgumentResolutionFailed(this ILogger logger, string moduleId, string errors);

    // ── Environment lifecycle ─────────────────────────────────────────────────

    [ZLoggerMessage(LogLevel.Debug, "Created {scope} environment '{name}'")]
    public static partial void LogEnvironmentCreated(this ILogger logger, string scope, string name);

    [ZLoggerMessage(LogLevel.Debug, "Resolved named environment reference '{name}'")]
    public static partial void LogNamedEnvironmentResolved(this ILogger logger, string name);

    [ZLoggerMessage(LogLevel.Debug, "Applied override resolution tags [{tags}] to environment '{environmentName}'")]
    public static partial void LogOverrideTagsApplied(this ILogger logger, string tags, string environmentName);

    // ── Artifact publishing ───────────────────────────────────────────────────

    [ZLoggerMessage(LogLevel.Debug, "Publishing artifacts for module '{moduleId}' to {targetScope} environment")]
    public static partial void LogArtifactPublishing(this ILogger logger, string moduleId, string targetScope);
}
