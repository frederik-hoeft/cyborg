using Cyborg.Cli.Arguments;
using Cyborg.Cli.Metrics;
using Cyborg.Core.Configuration;
using Cyborg.Core.Configuration.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Cli.Tests.Configuration;

[TestClass]
public sealed class ConfigurationArgumentHandlerTests : CyborgCliTestBase
{
    [TestMethod]
    public Task Test_TryProcessArgument_UntypedValue_AddsStringEntryAsync() => TestWithDIAsync(
        assertion: services =>
        {
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();
            Assert.AreEqual("console", configuration.Get<string>("cyborg.core.debug:frontend"));
        },
        buildConfiguration: configuration =>
        {
            IConfigurationArgumentHandler handler = configuration.ServiceProvider.GetRequiredService<IConfigurationArgumentHandler>();
            Assert.IsTrue(handler.TryProcessArgument(["cyborg.core.debug:frontend=console"], configuration, out _, out _));
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
            Assert.IsTrue(handler.TryProcessArgument(["test=first", "test=second"], configuration, out _, out _));
        });

    [TestMethod]
    public Task Test_TryProcessArgument_TypedPrimitive_UsesDynamicValueProviderAsync() => TestWithDIAsync(
        assertion: services =>
        {
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();
            Assert.IsTrue(configuration.TryGetValue("test:enabled", out bool enabled));
            Assert.IsTrue(enabled);
        },
        buildConfiguration: configuration =>
        {
            IConfigurationArgumentHandler handler = configuration.ServiceProvider.GetRequiredService<IConfigurationArgumentHandler>();
            Assert.IsTrue(handler.TryProcessArgument(["test:enabled::bool=true"], configuration, out _, out _));
        });

    [TestMethod]
    public Task Test_TryProcessArgument_TypedStructuredValue_DecomposesConfigurationEntryAsync() => TestWithDIAsync(
        assertion: services =>
        {
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();
            MetricsOptions options = configuration.Get<MetricsOptions>("cyborg.services.metrics")!;

            Assert.AreEqual("test", options.Namespace);
            Assert.AreEqual("/tmp/cyborg.prom", options.FilePath);
            Assert.AreEqual("test", configuration.Get<string>("cyborg.services.metrics:namespace"));
            Assert.AreEqual("/tmp/cyborg.prom", configuration.Get<string>("cyborg.services.metrics:file_path"));
        },
        buildConfiguration: configuration =>
        {
            IConfigurationArgumentHandler handler = configuration.ServiceProvider.GetRequiredService<IConfigurationArgumentHandler>();
            Assert.IsTrue(handler.TryProcessArgument(
                ["cyborg.services.metrics::cyborg.types.services.metrics.v1={\"namespace\":\"test\",\"file_path\":\"/tmp/cyborg.prom\"}"],
                configuration,
                out _,
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
            bool configured = handler.TryProcessArgument(["first=value", "second::bool=not-json"], configuration, out string? invalidDefinition, out string? errorMessage);

            Assert.IsFalse(configured);
            Assert.AreEqual("second::bool=not-json", invalidDefinition);
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
        });

    [TestMethod]
    public Task Test_TryProcessArgument_UnknownType_ReturnsDiagnosticAsync() => TestWithDIAsync(services =>
    {
        IConfigurationArgumentHandler handler = services.GetRequiredService<IConfigurationArgumentHandler>();
        IConfigurationBuilder configurationBuilder = services.GetRequiredService<IConfigurationBuilder>();

        bool configured = handler.TryProcessArgument(["test::does.not.exist=1"], configurationBuilder, out string? invalidDefinition, out string? errorMessage);

        Assert.IsFalse(configured);
        Assert.AreEqual("test::does.not.exist=1", invalidDefinition);
        Assert.IsNotNull(errorMessage);
        Assert.Contains("Unknown dynamic value type", errorMessage);
    });
}
