using Cyborg.Core.Logging;
using Cyborg.Core.TestAdapter.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cyborg.Core.TestAdapter;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection self)
    {
        public IServiceCollection AddDefaultTestServices()
        {
            self.TryAddSingleton(JsonNamingPolicy.SnakeCaseLower);
            self.AddSingleton<ILoggingConfigurator, TestLoggingConfigurator>();
            self.AddSingleton(static services =>
            {
                IEnumerable<ILoggingConfigurator> configurators = services.GetServices<ILoggingConfigurator>();
                return LoggerFactory.Create(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Trace);
                    foreach (ILoggingConfigurator configurator in configurators)
                    {
                        configurator.Configure(builder);
                    }
                });
            });
            return self;
        }
    }
}
