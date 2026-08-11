using Cyborg.Cli.Arguments;
using Cyborg.Core.Modules.Runtime.Environments;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Cli.Tests.Configuration;

[TestClass]
public sealed class EnvironmentVariableArgumentHandlerTests : CyborgCliTestBase
{
    [TestMethod]
    public Task Test_TryProcessArgument_TypedValue_UsesSharedDynamicParserAsync() => TestWithDIAsync(services =>
    {
        IEnvironmentVariableArgumentHandler handler = services.GetRequiredService<IEnvironmentVariableArgumentHandler>();
        GlobalRuntimeEnvironment environment = services.GetRequiredService<GlobalRuntimeEnvironment>();

        Assert.IsTrue(handler.TryProcessArgument(["port:int=2222"], environment));
        Assert.IsTrue(environment.TryResolveVariable("port", out int value));
        Assert.AreEqual(2222, value);
    });

    [TestMethod]
    public Task Test_TryProcessArgument_InvalidTypedValue_ReturnsFalseAsync() => TestWithDIAsync(services =>
    {
        IEnvironmentVariableArgumentHandler handler = services.GetRequiredService<IEnvironmentVariableArgumentHandler>();
        GlobalRuntimeEnvironment environment = services.GetRequiredService<GlobalRuntimeEnvironment>();

        Assert.IsFalse(handler.TryProcessArgument(["port:int=not-json"], environment));
        Assert.IsFalse(environment.TryResolveVariable("port", out int _));
    });
}
