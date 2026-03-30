using Cyborg.Cli.Logging.Options;
using Cyborg.Core.Configuration;
using Cyborg.Core.Logging;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ZLogger;

namespace Cyborg.Cli.Logging;

internal sealed class FileLoggingConfigurator(IConfiguration configuration) : ILoggingConfigurator
{
    public void Configure(ILoggingBuilder builder)
    {
        FileLoggingConfiguratorOptions options = configuration.Get("cyborg.services.logging.file", () => new FileLoggingConfiguratorOptions() { Enabled = false });
        if (!options.Enabled)
        {
            return;
        }
        File.Delete(options.Path);
        builder.AddZLoggerFile(options.Path, fileOptions =>
        {
            if (options.Format is LogFormat.Json)
            {
                fileOptions.UseJsonFormatter(f => f.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
            }
        });
    }
}