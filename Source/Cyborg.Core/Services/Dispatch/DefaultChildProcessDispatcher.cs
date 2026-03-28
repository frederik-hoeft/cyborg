using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Cyborg.Core.Services.Dispatch;

public sealed class DefaultChildProcessDispatcher(ILoggerFactory loggerFactory) : IChildProcessDispatcher
{
    private readonly ILogger<DefaultChildProcessDispatcher> _logger = loggerFactory.CreateLogger<DefaultChildProcessDispatcher>();

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
        try
        {
            process.Start();
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
        return childProcessResult;
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
