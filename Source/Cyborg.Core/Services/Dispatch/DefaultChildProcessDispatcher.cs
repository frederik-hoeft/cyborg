using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Cyborg.Core.Services.Dispatch;

public sealed class DefaultChildProcessDispatcher(ILoggerFactory loggerFactory) : IChildProcessDispatcher
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("cyborg.core.services.childprocess");

    public async Task<ChildProcessResult> ExecuteAsync(ProcessStartInfo processStartInfo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processStartInfo);
        // always disable shell execution to ensure that we can redirect streams and kill the process tree if needed
        processStartInfo.UseShellExecute = false;
        using Process process = new()
        {
            StartInfo = processStartInfo,
        };
        bool readStdout = processStartInfo.RedirectStandardOutput;
        bool readStderr = processStartInfo.RedirectStandardError;
        List<Task<CommandOutput>> ioTasks = [];
        string executable = processStartInfo.FileName;
        // join for display only — individual arguments are passed unmodified to the OS
        string arguments = string.Join(" ", processStartInfo.ArgumentList);
        _logger.LogProcessLaunching(executable, arguments);
        try
        {
            try
            {
                process.Start();
            }
            catch (Exception e)
            {
                _logger.LogProcessStartFailed(executable, e);
                throw;
            }
            _logger.LogProcessStarted(executable);
            if (readStdout)
            {
                ioTasks.Add(ReadStreamAsync(process.StandardOutput, static (result, data) => result.StandardOutput = data, cancellationToken));
            }
            if (readStderr)
            {
                ioTasks.Add(ReadStreamAsync(process.StandardError, static (result, data) => result.StandardError = data, cancellationToken));
            }
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(ioTasks);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    _logger.LogProcessKilled(executable);
                }
            }
            catch (InvalidOperationException)
            {
                // The process has already exited, so we can ignore this exception.
            }
            throw;
        }
        SubprocessResultBuilder builder = new();
        foreach (CommandOutput io in ioTasks.Select(t => t.Result))
        {
            io.CollectResult(builder);
        }
        ChildProcessResult childProcessResult = builder.Build(process.ExitCode);
        _logger.LogProcessExited(executable, childProcessResult.ExitCode);
        LogCapturedOutput(executable, childProcessResult);
        return childProcessResult;
    }

    private void LogCapturedOutput(string executable, ChildProcessResult childProcessResult)
    {
        if (childProcessResult.StandardOutput is not null)
        {
            _logger.LogProcessStandardOutput(executable, EscapeNewlines(childProcessResult.StandardOutput));
        }

        if (childProcessResult.StandardError is not null)
        {
            _logger.LogProcessStandardError(executable, EscapeNewlines(childProcessResult.StandardError));
        }
    }

    private static string EscapeNewlines(string value)
    {
        return value
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static async Task<CommandOutput> ReadStreamAsync(StreamReader reader, Action<SubprocessResultBuilder, string> setResult, CancellationToken cancellationToken)
    {
        string data = await reader.ReadToEndAsync(cancellationToken);
        return new CommandOutput(data, setResult);
    }

    private sealed class SubprocessResultBuilder
    {
        public string? StandardOutput { get; set; }

        public string? StandardError { get; set; }

        public ChildProcessResult Build(int exitCode) => new(exitCode, StandardOutput, StandardError);
    }

    private sealed class CommandOutput(string data, Action<SubprocessResultBuilder, string> setResult)
    {
        public void CollectResult(SubprocessResultBuilder builder) => setResult(builder, data);
    }
}
