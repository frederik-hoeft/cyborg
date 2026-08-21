using Cyborg.Cli.Arguments;
using Cyborg.Cli.Configuration;
using Cyborg.Cli.Logging.Options;
using Cyborg.Core.Configuration;
using Cyborg.Core.Configuration.Builders;
using Cyborg.Core.Services.Security.Trust.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZLogger.Providers;

namespace Cyborg.Cli.Tests.Configuration;

[TestClass]
public sealed class ConfigurationArgumentHandlerTests : CyborgCliTestBase
{
    [TestMethod]
    public Task Test_TryProcessArgument_UntypedValue_AddsStringEntryAsync() => TestWithDIAsync(
        assertion: services =>
        {
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();
            Assert.AreEqual("console", configuration.Get<string>(CliConfigurationDefaults.DEBUG_FRONTEND_KEY));
        },
        buildConfiguration: configuration =>
        {
            IConfigurationArgumentHandler handler = configuration.ServiceProvider.GetRequiredService<IConfigurationArgumentHandler>();
            Assert.IsTrue(handler.TryProcessArgument(["cyborg.core.debug.frontend=console"], configuration, out _));
        });

    [TestMethod]
    public Task Test_TryProcessArgument_DuplicateKey_LastValueWinsAsync() => TestWithDIAsync(
        assertion: services =>
        {
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();
            Assert.AreEqual("second", configuration.Get<string>("test"));
        },
        buildConfiguration: configuration =>
        {
            IConfigurationArgumentHandler handler = configuration.ServiceProvider.GetRequiredService<IConfigurationArgumentHandler>();
            Assert.IsTrue(handler.TryProcessArgument(["test=first", "test=second"], configuration, out _));
        });

    [TestMethod]
    public Task Test_TryProcessArgument_TypedPrimitive_UsesDynamicValueProviderAsync() => TestWithDIAsync(
        assertion: services =>
        {
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();
            Assert.IsTrue(configuration.TryGetValue("test.enabled", out bool enabled));
            Assert.IsTrue(enabled);
        },
        buildConfiguration: configuration =>
        {
            IConfigurationArgumentHandler handler = configuration.ServiceProvider.GetRequiredService<IConfigurationArgumentHandler>();
            Assert.IsTrue(handler.TryProcessArgument(["test.enabled:bool=true"], configuration, out _));
        });

    [TestMethod]
    public Task Test_TryProcessArgument_TypedStructuredValue_ContributesLeavesOnlyAsync() => TestWithDIAsync(
        assertion: services =>
        {
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();

            Assert.IsNull(configuration[CliConfigurationDefaults.METRICS_OPTIONS_KEY]);
            Assert.AreEqual("test", configuration.Get<string>(CliConfigurationDefaults.METRICS_NAMESPACE_KEY));
            Assert.AreEqual("/tmp/cyborg.prom", configuration.Get<string>(CliConfigurationDefaults.METRICS_FILE_PATH_KEY));
        },
        buildConfiguration: configuration =>
        {
            IConfigurationArgumentHandler handler = configuration.ServiceProvider.GetRequiredService<IConfigurationArgumentHandler>();
            Assert.IsTrue(handler.TryProcessArgument(
                ["cyborg.services.metrics:cyborg.types.services.metrics.v1={\"namespace\":\"test\",\"file_path\":\"/tmp/cyborg.prom\"}"],
                configuration,
                out _));
        });

    [TestMethod]
    public Task Test_TryProcessArgument_EnumValues_UseDedicatedProvidersAsync() => TestWithDIAsync(
        assertion: services =>
        {
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();

            Assert.AreEqual(LogLevel.Warning, configuration.Get(CliConfigurationDefaults.GLOBAL_LOGGING_MINIMUM_LEVEL_KEY, LogLevel.None));
            Assert.AreEqual(LogFormat.Json, configuration.Get(CliConfigurationDefaults.CONSOLE_LOGGING_FORMAT_KEY, LogFormat.Text));
            Assert.IsTrue(configuration.TryGetValue(CliConfigurationDefaults.ROLLING_LOGGING_INTERVAL_KEY, out RollingInterval rollingInterval));
            Assert.AreEqual(RollingInterval.Day, rollingInterval);
            Assert.AreEqual(TrustEnforcementMode.LogOnly, configuration.Get(CliConfigurationDefaults.TRUST_ENFORCEMENT_MODE_KEY, TrustEnforcementMode.Enforce));
        },
        buildConfiguration: configuration =>
        {
            IConfigurationArgumentHandler handler = configuration.ServiceProvider.GetRequiredService<IConfigurationArgumentHandler>();
            Assert.IsTrue(handler.TryProcessArgument(
                [
                    "cyborg.services.logging.minimum_level:cyborg.types.services.logging.level.v1=\"warning\"",
                    "cyborg.services.logging.console.format:cyborg.types.services.logging.format.v1=\"json\"",
                    "cyborg.services.logging.rolling.rolling_interval:cyborg.types.services.logging.rolling_interval.v1=\"day\"",
                    "cyborg.services.trust.enforcement_mode:cyborg.types.services.trust.enforcement_mode.v1=\"log_only\"",
                ],
                configuration,
                out _));
        });

    [TestMethod]
    public Task Test_TryProcessArgument_InvalidTypedValue_DoesNotAddPartialSourceAsync() => TestWithDIAsync(
        assertion: services =>
        {
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();
            Assert.IsNull(configuration["first"]);
            Assert.IsNull(configuration["second"]);
        },
        buildConfiguration: configuration =>
        {
            IConfigurationArgumentHandler handler = configuration.ServiceProvider.GetRequiredService<IConfigurationArgumentHandler>();
            bool configured = handler.TryProcessArgument(["first=value", "second:bool=not-json"], configuration, out string? errorMessage);

            Assert.IsFalse(configured);
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
        });

    [TestMethod]
    public Task Test_TryProcessArgument_UnknownType_ReturnsDiagnosticAsync() => TestWithDIAsync(services =>
    {
        IConfigurationArgumentHandler handler = services.GetRequiredService<IConfigurationArgumentHandler>();
        IConfigurationBuilder configurationBuilder = services.GetRequiredService<IConfigurationBuilder>();

        bool configured = handler.TryProcessArgument(["test:does.not.exist=1"], configurationBuilder, out string? errorMessage);

        Assert.IsFalse(configured);
        Assert.IsNotNull(errorMessage);
        Assert.Contains("test:does.not.exist=1", errorMessage);
        Assert.Contains("Reason:", errorMessage);
        Assert.Contains("Unknown dynamic value type", errorMessage);
    });
}
