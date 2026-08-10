using Cyborg.Cli.Configuration;
using Cyborg.Cli.Logging;
using Cyborg.Cli.Metrics;
using Cyborg.Core.Configuration;
using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Services.Default;
using Cyborg.Core.Services.Security.Trust.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cyborg.Cli.Tests.Configuration;

[TestClass]
public sealed class CliConfigurationServiceTests : CyborgCliTestBase
{
    [TestMethod]
    public async Task Test_TryConfigure_DefaultsAreAppliedWithoutFileOverrideAsync()
    {
        string optionsPath = await CreateOptionsFileAsync(frontend: null);
        try
        {
            await TestWithDIAsync(
                assertion: services =>
                {
                    IConfiguration configuration = services.GetRequiredService<IConfiguration>();
                    GlobalLoggingOptions globalLogging = configuration.Get<GlobalLoggingOptions>(CliConfigurationDefaults.GLOBAL_LOGGING_OPTIONS_KEY)!;
                    ConsoleLoggingConfiguratorOptions consoleLogging = configuration.Get<ConsoleLoggingConfiguratorOptions>(CliConfigurationDefaults.CONSOLE_LOGGING_OPTIONS_KEY)!;
                    FileLoggingConfiguratorOptions fileLogging = configuration.Get<FileLoggingConfiguratorOptions>(CliConfigurationDefaults.FILE_LOGGING_OPTIONS_KEY)!;
                    RollingFileLoggingConfiguratorOptions rollingLogging = configuration.Get<RollingFileLoggingConfiguratorOptions>(CliConfigurationDefaults.ROLLING_LOGGING_OPTIONS_KEY)!;
                    ConfigurationTrustOptions trust = configuration.Get<ConfigurationTrustOptions>(CliConfigurationDefaults.TRUST_OPTIONS_KEY)!;

                    Assert.AreEqual("console", configuration.Get<string>("cyborg.core.debug:frontend"));
                    Assert.AreEqual(LogLevel.Trace, globalLogging.MinimumLevel);
                    Assert.IsFalse(consoleLogging.Enabled);
                    Assert.IsFalse(fileLogging.Enabled);
                    Assert.IsFalse(rollingLogging.Enabled);
                    Assert.AreEqual("cyborg", configuration.Get<string>("cyborg.services.metrics:namespace"));
                    Assert.AreEqual(TrustEnforcementMode.Enforce, trust.EnforcementMode);
                    Assert.AreEqual(0, trust.Policies.Count);
                },
                buildConfiguration: configuration =>
                {
                    ICliConfigurationService service = configuration.ServiceProvider.GetRequiredService<ICliConfigurationService>();
                    Assert.IsTrue(service.TryConfigure(configuration, optionsPath, configurationEntries: null, out _, out _));
                });
        }
        finally
        {
            File.Delete(optionsPath);
        }
    }

    [TestMethod]
    public async Task Test_TryConfigure_ConfigFileOverridesBuiltInDefaultsAsync()
    {
        string optionsPath = await CreateOptionsFileAsync("custom");
        try
        {
            await TestWithDIAsync(
                assertion: services =>
                {
                    IConfiguration configuration = services.GetRequiredService<IConfiguration>();
                    IDefault<IDebugFrontend> defaultFrontend = services.GetRequiredService<IDefault<IDebugFrontend>>();

                    Assert.AreEqual("custom", configuration.Get<string>("cyborg.core.debug:frontend"));
                    Assert.AreEqual("custom", defaultFrontend.GetRequiredDefault().Key);
                },
                configureServices: static services => services.AddSingleton<IDebugFrontend>(new CustomDebugFrontend()),
                buildConfiguration: configuration =>
                {
                    ICliConfigurationService service = configuration.ServiceProvider.GetRequiredService<ICliConfigurationService>();
                    Assert.IsTrue(service.TryConfigure(configuration, optionsPath, configurationEntries: null, out _, out _));
                });
        }
        finally
        {
            File.Delete(optionsPath);
        }
    }

    [TestMethod]
    public async Task Test_TryConfigure_TypedCliArgumentOverridesStructuredFileOptionAsync()
    {
        string optionsPath = await CreateMetricsOptionsFileAsync("file");
        try
        {
            await TestWithDIAsync(
                assertion: services =>
                {
                    IConfiguration configuration = services.GetRequiredService<IConfiguration>();
                    MetricsOptions options = configuration.Get<MetricsOptions>(CliConfigurationDefaults.METRICS_OPTIONS_KEY)!;

                    Assert.AreEqual("cli", options.Namespace);
                    Assert.AreEqual("/tmp/cli.prom", options.FilePath);
                },
                buildConfiguration: configuration =>
                {
                    ICliConfigurationService service = configuration.ServiceProvider.GetRequiredService<ICliConfigurationService>();
                    Assert.IsTrue(service.TryConfigure(
                        configuration,
                        optionsPath,
                        ["cyborg.services.metrics::cyborg.types.services.metrics.v1={\"namespace\":\"cli\",\"file_path\":\"/tmp/cli.prom\"}"],
                        out _,
                        out _));
                });
        }
        finally
        {
            File.Delete(optionsPath);
        }
    }

    [TestMethod]
    public async Task Test_TryConfigure_CliArgumentsOverrideConfigFileAsync()
    {
        string optionsPath = await CreateOptionsFileAsync("file");
        try
        {
            await TestWithDIAsync(
                assertion: services =>
                {
                    IConfiguration configuration = services.GetRequiredService<IConfiguration>();
                    Assert.AreEqual("cli", configuration.Get<string>("cyborg.core.debug:frontend"));
                },
                buildConfiguration: configuration =>
                {
                    ICliConfigurationService service = configuration.ServiceProvider.GetRequiredService<ICliConfigurationService>();
                    Assert.IsTrue(service.TryConfigure(configuration, optionsPath, ["cyborg.core.debug:frontend=cli"], out _, out _));
                });
        }
        finally
        {
            File.Delete(optionsPath);
        }
    }

    private static async Task<string> CreateOptionsFileAsync(string? frontend)
    {
        string path = Path.GetTempFileName();
        string options = frontend is null
            ? "{ \"options\": [] }"
            : $$"""
              {
                "options": [
                  {
                    "key": "cyborg.core.debug",
                    "cyborg.types.core.debug.options.v1": {
                      "frontend": "{{frontend}}"
                    }
                  }
                ]
              }
              """;
        await File.WriteAllTextAsync(path, options);
        return path;
    }

    private sealed class CustomDebugFrontend : IDebugFrontend
    {
        public string Key => "custom";

        public ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(DebugResumeAction.Continue);
    }

    private static async Task<string> CreateMetricsOptionsFileAsync(string @namespace)
    {
        string path = Path.GetTempFileName();
        string options = $$"""
          {
            "options": [
              {
                "key": "cyborg.services.metrics",
                "cyborg.types.services.metrics.v1": {
                  "namespace": "{{@namespace}}",
                  "file_path": "/tmp/file.prom"
                }
              }
            ]
          }
          """;
        await File.WriteAllTextAsync(path, options);
        return path;
    }
}
