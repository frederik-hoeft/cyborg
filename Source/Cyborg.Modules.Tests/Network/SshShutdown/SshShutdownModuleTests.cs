using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Services.Dispatch;
using Cyborg.Modules.Network.SshShutdown;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cyborg.Modules.Tests.Network.SshShutdown;

[TestClass]
public sealed class SshShutdownModuleTests : ModuleTestBase
{
    private const string MODULE_JSON = """
        {
          "cyborg.modules.network.ssh_shutdown.v1": {
            "executable": "/bin/true",
            "hostname": "backup.example",
            "username": "root",
            "shutdown_command": "/usr/bin/shutdown -h now"
          }
        }
        """;

    [TestMethod]
    public Task ExecuteAsync_ExitZero_CapturesAndPublishesProcessOutputAsync()
    {
        RecordingChildProcessDispatcher dispatcher = new(new ChildProcessResult(0, "shutdown scheduled", "ssh banner"));

        return TestExecutionAsync(
            MODULE_JSON,
            result =>
            {
                MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
                AssertCaptureRequested(dispatcher);
                AssertResultArtifacts(result, 0, "shutdown scheduled", "ssh banner");
            },
            configureServices: services => services.AddSingleton<IChildProcessDispatcher>(dispatcher));
    }

    [TestMethod]
    [DataRow(7, "shutdown: permission denied")]
    [DataRow(255, "Permission denied (publickey).")]
    [DataRow(255, "Connection to backup.example closed by remote host.")]
    public Task ExecuteAsync_NonZeroExit_RemainsFailedAndLogsCapturedErrorAsync(int exitCode, string standardError)
    {
        RecordingChildProcessDispatcher dispatcher = new(new ChildProcessResult(exitCode, string.Empty, standardError));
        using RecordingLoggerFactory loggerFactory = new();

        return TestExecutionAsync(
            MODULE_JSON,
            result =>
            {
                MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status);
                AssertCaptureRequested(dispatcher);
                AssertResultArtifacts(result, exitCode, string.Empty, standardError);
                MSAssert.IsTrue(loggerFactory.Messages.Any(message => message.Contains(standardError, StringComparison.Ordinal)));
                MSAssert.IsTrue(loggerFactory.Messages.Any(message => message.Contains($"exit code {exitCode}", StringComparison.Ordinal)));
            },
            configureServices: services =>
            {
                services.AddSingleton<IChildProcessDispatcher>(dispatcher);
                services.AddSingleton<ILoggerFactory>(loggerFactory);
            });
    }

    private static void AssertCaptureRequested(RecordingChildProcessDispatcher dispatcher)
    {
        ChildProcessInvocation invocation = dispatcher.Invocation ?? throw new AssertFailedException("SSH shutdown did not invoke the child-process dispatcher.");
        MSAssert.IsTrue(invocation.RedirectStandardOutput);
        MSAssert.IsTrue(invocation.RedirectStandardError);
    }

    private static void AssertResultArtifacts(IModuleExecutionResult result, int exitCode, string standardOutput, string standardError)
    {
        MSAssert.IsTrue(result.Artifacts.TryResolveVariable($"{SshShutdownModule.ModuleId}.exit_code", out int actualExitCode));
        MSAssert.AreEqual(exitCode, actualExitCode);
        MSAssert.IsTrue(result.Artifacts.TryResolveVariable($"{SshShutdownModule.ModuleId}.standard_output", out string? actualStandardOutput));
        MSAssert.AreEqual(standardOutput, actualStandardOutput);
        MSAssert.IsTrue(result.Artifacts.TryResolveVariable($"{SshShutdownModule.ModuleId}.standard_error", out string? actualStandardError));
        MSAssert.AreEqual(standardError, actualStandardError);
    }

    private sealed class RecordingChildProcessDispatcher(ChildProcessResult result) : IChildProcessDispatcher
    {
        private readonly ChildProcessResult _result = result;

        public ChildProcessInvocation? Invocation { get; private set; }

        public Task<ChildProcessResult> ExecuteAsync(ChildProcessInvocation invocation, CancellationToken cancellationToken)
        {
            Invocation = invocation;
            return Task.FromResult(_result);
        }
    }

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages => _messages;

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(_messages);

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            messages.Add(formatter(state, exception));
        }
    }
}
