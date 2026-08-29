using Cyborg.Cli.Debugging;
using Cyborg.Core.Configuration;
using Cyborg.Core.Runtime.Services.Debugging.Breakpoints;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Cli.Tests.Debugging;

[TestClass]
public sealed class CliDebugArgumentHandlerTests : CyborgCliTestBase
{
    [TestMethod]
    public Task Test_TryConfigure_BreakAtDoesNotSelectFrontendAsync() => TestWithDIAsync(
        assertion: services =>
        {
            ICliDebugArgumentHandler handler = services.GetRequiredService<ICliDebugArgumentHandler>();
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();
            IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();

            Assert.IsFalse(configuration.TryGetValue(handler.FrontendConfigurationKey, out string? _));
            Assert.AreEqual(1, breakpoints.Count);
            Assert.IsFalse(handler.HasUsableFrontend());
        },
        buildConfiguration: configuration =>
        {
            ICliDebugArgumentHandler handler = configuration.ServiceProvider.GetRequiredService<ICliDebugArgumentHandler>();
            Assert.IsTrue(handler.TryConfigure(["probe"], out _, out _));
        });

    [TestMethod]
    public Task Test_TryConfigure_InvalidBreakpoint_ReturnsDiagnosticAsync() => TestWithDIAsync(services =>
    {
        ICliDebugArgumentHandler handler = services.GetRequiredService<ICliDebugArgumentHandler>();
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();

        bool configured = handler.TryConfigure(["probe", "["], out string? invalidExpression, out string? errorMessage);

        Assert.IsFalse(configured);
        Assert.AreEqual("[", invalidExpression);
        Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
        Assert.AreEqual(0, breakpoints.Count);
    });
}
