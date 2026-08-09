using Cyborg.Core.Logging;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;

namespace Cyborg.Core.TestAdapter.Logging;

internal sealed class TestLoggingConfigurator(TestContext testContext) : ILoggingConfigurator
{
    public void Configure(ILoggingBuilder builder)
    {
        builder.AddFilter<ZLoggerInMemoryLoggerProvider>(category: null, LogLevel.Trace);

        builder.AddZLoggerInMemory(processor => processor.MessageReceived += testContext.WriteLine);
    }
}
