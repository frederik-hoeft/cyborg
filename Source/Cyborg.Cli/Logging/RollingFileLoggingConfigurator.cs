using Cyborg.Core.Logging;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;

namespace Cyborg.Cli.Logging;

internal sealed class RollingFileLoggingConfigurator : ILoggingConfigurator
{
    public void Configure(ILoggingBuilder builder) =>
        builder.AddZLoggerRollingFile(options =>
        {
            options.FilePathSelector = (timestamp, sequenceNumber) =>
                $"logs/{timestamp.ToLocalTime():yyyy-MM-dd}_{sequenceNumber:000}.log";
            options.RollingInterval = RollingInterval.Day;
            options.RollingSizeKB = 10 * 1024;
            options.UseJsonFormatter();
        });
}
