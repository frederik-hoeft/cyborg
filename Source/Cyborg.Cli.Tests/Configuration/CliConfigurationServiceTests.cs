using Cyborg.Cli.Configuration;
using Cyborg.Core.Configuration;
using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Runtime.Services.Debugging;
using Cyborg.Core.Services.Default;
using Cyborg.Core.Services.Security.Trust.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cyborg.Cli.Tests.Configuration;

[TestClass]
public sealed class CliConfigurationServiceTests : CyborgCliTestBase
{
    [TestMethod]
    public async Task Test_TryConfigure_DefaultsAreAppliedAsLeavesWithoutFileOverrideAsync()
    {
        string optionsPath = await CreateOptionsFileAsync(frontend: null);
        try
        {
            await TestWithDIAsync(
                assertion: services =>
                {
                    IConfiguration configuration = services.GetRequiredService<IConfiguration>();

                    Assert.IsNull(configuration[CliConfigurationDefaults.DEBUG_OPTIONS_KEY]);
                    Assert.IsNull(configuration[CliConfigurationDefaults.GLOBAL_LOGGING_OPTIONS_KEY]);
                    Assert.IsNull(configuration[CliConfigurationDefaults.CONSOLE_LOGGING_OPTIONS_KEY]);
                    Assert.IsNull(configuration[CliConfigurationDefaults.TRUST_OPTIONS_KEY]);
                    Assert.AreEqual("console", configuration.Get<string>(CliConfigurationDefaults.DEBUG_FRONTEND_KEY));
                    Assert.AreEqual(LogLevel.Trace, configuration.Get(CliConfigurationDefaults.GLOBAL_LOGGING_MINIMUM_LEVEL_KEY, LogLevel.None));
                    Assert.IsFalse(configuration.Get(CliConfigurationDefaults.CONSOLE_LOGGING_ENABLED_KEY, true));
                    Assert.AreEqual(LogLevel.Information, configuration.Get(CliConfigurationDefaults.CONSOLE_LOGGING_MINIMUM_LEVEL_KEY, LogLevel.None));
                    Assert.IsFalse(configuration.Get(CliConfigurationDefaults.FILE_LOGGING_ENABLED_KEY, true));
                    Assert.IsFalse(configuration.Get(CliConfigurationDefaults.ROLLING_LOGGING_ENABLED_KEY, true));
                    Assert.AreEqual("cyborg", configuration.Get<string>(CliConfigurationDefaults.METRICS_NAMESPACE_KEY));
                    Assert.AreEqual(TrustEnforcementMode.Enforce, configuration.Get(CliConfigurationDefaults.TRUST_ENFORCEMENT_MODE_KEY, TrustEnforcementMode.Disabled));
                    IReadOnlyList<DynamicValue> policies = configuration.Get<IReadOnlyList<DynamicValue>>(CliConfigurationDefaults.TRUST_POLICIES_KEY)!;
                    Assert.AreEqual(0, policies.Count);
                },
                buildConfiguration: configuration =>
                {
                    ICliConfigurationService service = configuration.ServiceProvider.GetRequiredService<ICliConfigurationService>();
                    Assert.IsTrue(service.TryConfigure(configuration, optionsPath, configurationEntries: null, out _));
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

                    Assert.AreEqual("custom", configuration.Get<string>(CliConfigurationDefaults.DEBUG_FRONTEND_KEY));
                    Assert.AreEqual("custom", defaultFrontend.GetRequiredDefault().Key);
                },
                configureServices: static services => services.AddSingleton<IDebugFrontend>(new CustomDebugFrontend()),
                buildConfiguration: configuration =>
                {
                    ICliConfigurationService service = configuration.ServiceProvider.GetRequiredService<ICliConfigurationService>();
                    Assert.IsTrue(service.TryConfigure(configuration, optionsPath, configurationEntries: null, out _));
                });
        }
        finally
        {
            File.Delete(optionsPath);
        }
    }

    [TestMethod]
    public async Task Test_TryConfigure_LeafCliArgumentOverridesStructuredFileOptionAsync()
    {
        string optionsPath = await CreateMetricsOptionsFileAsync("file");
        try
        {
            await TestWithDIAsync(
                assertion: services =>
                {
                    IConfiguration configuration = services.GetRequiredService<IConfiguration>();

                    Assert.IsNull(configuration[CliConfigurationDefaults.METRICS_OPTIONS_KEY]);
                    Assert.AreEqual("file", configuration.Get<string>(CliConfigurationDefaults.METRICS_NAMESPACE_KEY));
                    Assert.AreEqual("/tmp/cli.prom", configuration.Get<string>(CliConfigurationDefaults.METRICS_FILE_PATH_KEY));
                },
                buildConfiguration: configuration =>
                {
                    ICliConfigurationService service = configuration.ServiceProvider.GetRequiredService<ICliConfigurationService>();
                    Assert.IsTrue(service.TryConfigure(
                        configuration,
                        optionsPath,
                        ["cyborg.services.metrics.file_path=/tmp/cli.prom"],
                        out _));
                });
        }
        finally
        {
            File.Delete(optionsPath);
        }
    }

    [TestMethod]
    public async Task Test_TryConfigure_TypedStructuredCliArgumentContributesLeavesOnlyAsync()
    {
        string optionsPath = await CreateMetricsOptionsFileAsync("file");
        try
        {
            await TestWithDIAsync(
                assertion: services =>
                {
                    IConfiguration configuration = services.GetRequiredService<IConfiguration>();

                    Assert.IsNull(configuration[CliConfigurationDefaults.METRICS_OPTIONS_KEY]);
                    Assert.AreEqual("cli", configuration.Get<string>(CliConfigurationDefaults.METRICS_NAMESPACE_KEY));
                    Assert.AreEqual("/tmp/cli.prom", configuration.Get<string>(CliConfigurationDefaults.METRICS_FILE_PATH_KEY));
                },
                buildConfiguration: configuration =>
                {
                    ICliConfigurationService service = configuration.ServiceProvider.GetRequiredService<ICliConfigurationService>();
                    Assert.IsTrue(service.TryConfigure(
                        configuration,
                        optionsPath,
                        ["cyborg.services.metrics:cyborg.types.services.metrics.v1={\"namespace\":\"cli\",\"file_path\":\"/tmp/cli.prom\"}"],
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
                    Assert.AreEqual("cli", configuration.Get<string>(CliConfigurationDefaults.DEBUG_FRONTEND_KEY));
                },
                buildConfiguration: configuration =>
                {
                    ICliConfigurationService service = configuration.ServiceProvider.GetRequiredService<ICliConfigurationService>();
                    Assert.IsTrue(service.TryConfigure(configuration, optionsPath, ["cyborg.core.debug.frontend=cli"], out _));
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

    private sealed class CustomDebugFrontend : IDebugFrontend
    {
        public string Key => "custom";

        public ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(DebugResumeAction.Continue);
    }
}
