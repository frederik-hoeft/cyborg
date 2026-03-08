using System.Diagnostics;

namespace Cyborg.Core.Services.Subprocesses;

public sealed class DefaultSubprocessDispatcher : ISubprocessDispatcher
{
    public async Task<SubprocessResult> ExecuteAsync(ProcessStartInfo processStartInfo, CancellationToken cancellationToken)
    {
        using Process process = new()
        {
            StartInfo = processStartInfo,
        };
        bool readStdout = processStartInfo.RedirectStandardOutput;
        bool readStderr = processStartInfo.RedirectStandardError;
        List<Task<CommandOutput>> ioTasks = [];
        try
        {
            process.Start();
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
        return builder.Build(process.ExitCode);
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

        public SubprocessResult Build(int exitCode) => new(exitCode, StandardOutput, StandardError);
    }

    private sealed class CommandOutput(string data, Action<SubprocessResultBuilder, string> setResult)
    {
        public void CollectResult(SubprocessResultBuilder builder) => setResult(builder, data);
    }
}
