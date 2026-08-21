using Cyborg.Core.Text;
using Cyborg.Core.Text.Rendering;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Cyborg.Core.Services.Dispatch;

public sealed class DefaultChildProcessDispatcher(ILoggerFactory loggerFactory, ITaggedStringRenderer taggedStringRenderer) : IChildProcessDispatcher
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("cyborg.core.services.childprocess");

    public Task<ChildProcessResult> ExecuteAsync(ChildProcessInvocation invocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ProcessStartInfo processStartInfo = invocation.CreateProcessStartInfo();
        string renderedArguments = string.Join(" ", invocation.ArgumentList.Select(taggedStringRenderer.Render));
        return ExecuteAsync(processStartInfo, renderedArguments, cancellationToken);
    }

    public Task<ChildProcessResult> ExecuteAsync(ProcessStartInfo processStartInfo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processStartInfo);
        // A raw ProcessStartInfo has already discarded any TaggedString metadata. Do not mirror its
        // arguments into logs because there is no safe way to determine how they should be rendered.
        return ExecuteAsync(processStartInfo, renderedArguments: null, cancellationToken);
    }

    private async Task<ChildProcessResult> ExecuteAsync(ProcessStartInfo processStartInfo, string? renderedArguments, CancellationToken cancellationToken)
    {
        // always disable shell execution to ensure that we can redirect streams and kill the process tree if needed
        processStartInfo.UseShellExecute = false;
        bool readStdout = processStartInfo.RedirectStandardOutput;
        bool readStderr = processStartInfo.RedirectStandardError;
        // always redirect both streams to prevent subprocess output from being inherited by Cyborg's process
        processStartInfo.RedirectStandardOutput = true;
        processStartInfo.RedirectStandardError = true;
        using Process process = new()
        {
            StartInfo = processStartInfo,
        };
        SubprocessResultBuilder builder = new();
        List<Task> streamTasks = [];
        string executable = processStartInfo.FileName;
        if (renderedArguments is null)
        {
            _logger.LogProcessLaunchingWithoutArguments(executable);
        }
        else
        {
            _logger.LogProcessLaunching(executable, renderedArguments);
        }
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
                streamTasks.Add(CaptureStreamAsync(process.StandardOutput, data => builder.StandardOutput = data, cancellationToken));
            }
            else
            {
                streamTasks.Add(DiscardStreamAsync(process.StandardOutput, cancellationToken));
            }
            if (readStderr)
            {
                streamTasks.Add(CaptureStreamAsync(process.StandardError, data => builder.StandardError = data, cancellationToken));
            }
            else
            {
                streamTasks.Add(DiscardStreamAsync(process.StandardError, cancellationToken));
            }
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(streamTasks);
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

    private static async Task CaptureStreamAsync(StreamReader reader, Action<string> setResult, CancellationToken cancellationToken)
    {
        string data = await reader.ReadToEndAsync(cancellationToken);
        setResult(data);
    }

    private static Task DiscardStreamAsync(StreamReader reader, CancellationToken cancellationToken) =>
        reader.BaseStream.CopyToAsync(Stream.Null, cancellationToken);

    private sealed class SubprocessResultBuilder
    {
        public string? StandardOutput { get; set; }

        public string? StandardError { get; set; }

        public ChildProcessResult Build(int exitCode) => new(exitCode, StandardOutput, StandardError);
    }
}
