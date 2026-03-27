using Cyborg.Cli.Logging;
using Cyborg.Core.Logging;
using Jab;
using Microsoft.Extensions.Logging;

namespace Cyborg.Cli;

[ServiceProviderModule]
[Singleton<ILoggingConfigurator, ConsoleLoggingConfigurator>]
[Singleton<ILoggingConfigurator, RollingFileLoggingConfigurator>]
[Singleton<ILoggerFactory>(Factory = nameof(CreateLoggerFactory))]
internal interface ICyborgCliLoggingServices
{
    static ILoggerFactory CreateLoggerFactory(IEnumerable<ILoggingConfigurator> configurators) =>
        LoggerFactory.Create(builder =>
        {
            foreach (ILoggingConfigurator configurator in configurators)
            {
                configurator.Configure(builder);
            }
        });
}
