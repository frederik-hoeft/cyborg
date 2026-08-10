using Cyborg.Cli.Debugging;
using Cyborg.Core.Configuration;
using Cyborg.Core.Configuration.Builders;
using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Services.Default;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Cli.Tests.Debugging;

[TestClass]
public sealed class CliDebugArgumentHandlerTests : CyborgCliTestBase
{
    [TestMethod]
    public Task Test_TryConfigure_BreakAtProvidesConsoleFrontendDefaultAsync() => TestWithDIAsync(
        assertion: services =>
        {
            ICliDebugArgumentHandler handler = services.GetRequiredService<ICliDebugArgumentHandler>();
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();

            Assert.IsTrue(configuration.TryGetValue(handler.FrontendConfigurationKey, out string? selectedFrontend));
            Assert.AreEqual("console", selectedFrontend);
            Assert.IsTrue(handler.HasUsableFrontend());
        },
        buildConfiguration: configuration =>
        {
            ICliDebugArgumentHandler handler = configuration.ServiceProvider.GetRequiredService<ICliDebugArgumentHandler>();
            Assert.IsTrue(handler.TryConfigure(configuration, ["probe"], out _, out _));
        });

    [TestMethod]
    public Task Test_TryConfigure_ExplicitFrontendSourceOverridesConsoleDefaultAsync() => TestWithDIAsync(
        assertion: services =>
        {
            ICliDebugArgumentHandler handler = services.GetRequiredService<ICliDebugArgumentHandler>();
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();
            IDefault<IDebugFrontend> defaultFrontend = services.GetRequiredService<IDefault<IDebugFrontend>>();

            Assert.IsTrue(configuration.TryGetValue(handler.FrontendConfigurationKey, out string? selectedFrontend));
            Assert.AreEqual("custom", selectedFrontend);
            Assert.AreEqual("custom", defaultFrontend.GetRequiredDefault().Key);
        },
        configureServices: static services => services.AddSingleton<IDebugFrontend>(new CustomDebugFrontend()),
        buildConfiguration: configuration =>
        {
            ICliDebugArgumentHandler handler = configuration.ServiceProvider.GetRequiredService<ICliDebugArgumentHandler>();
            Assert.IsTrue(handler.TryConfigure(configuration, ["probe"], out _, out _));
            configuration.AddDictionary(dictionary => dictionary.AddEntry(handler.FrontendConfigurationKey, "custom"));
        });

    [TestMethod]
    public Task Test_TryConfigure_InvalidBreakpoint_ReturnsDiagnosticAsync() => TestWithDIAsync(services =>
    {
        ICliDebugArgumentHandler handler = services.GetRequiredService<ICliDebugArgumentHandler>();
        IConfigurationBuilder configurationBuilder = services.GetRequiredService<IConfigurationBuilder>();
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();

        bool configured = handler.TryConfigure(configurationBuilder, ["probe", "["], out string? invalidExpression, out string? errorMessage);

        Assert.IsFalse(configured);
        Assert.AreEqual("[", invalidExpression);
        Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
        Assert.AreEqual(0, breakpoints.Count);
    });

    private sealed class CustomDebugFrontend : IDebugFrontend
    {
        public string Key => "custom";

        public ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(DebugResumeAction.Continue);
    }
}
