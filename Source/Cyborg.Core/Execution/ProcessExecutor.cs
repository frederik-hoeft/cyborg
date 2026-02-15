using System.Diagnostics;
using Cyborg.Core.Logging;

namespace Cyborg.Core.Execution;

public sealed record CommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool Success
);

public sealed class ProcessExecutor
{
    private readonly ILogger _logger;

    public ProcessExecutor(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<CommandResult> ExecuteAsync(
        string executable,
        string arguments,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null,
        CancellationToken cancellationToken = default)
    {
        _logger.Exec($"{executable} {arguments}");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
        };

        if (environmentVariables != null)
        {
            foreach (var (key, value) in environmentVariables)
            {
                startInfo.Environment[key] = value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        
        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                _logger.Info(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
                _logger.Warn(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        var exitCode = process.ExitCode;
        var stdout = outputBuilder.ToString();
        var stderr = errorBuilder.ToString();
        var success = exitCode == 0;

        if (!success)
        {
            _logger.Error($"Command failed with exit code {exitCode}");
        }

        return new CommandResult(exitCode, stdout, stderr, success);
    }
}
