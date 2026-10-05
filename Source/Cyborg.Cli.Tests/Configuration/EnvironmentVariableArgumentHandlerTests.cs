using Cyborg.Cli.Arguments;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Cli.Tests.Configuration;

[TestClass]
public sealed class EnvironmentVariableArgumentHandlerTests : CyborgCliTestBase
{
    [TestMethod]
    public Task Test_TryProcessArgument_TypedValue_UsesSharedDynamicParserAsync() => TestWithDIAsync(services =>
    {
        IEnvironmentVariableArgumentHandler handler = services.GetRequiredService<IEnvironmentVariableArgumentHandler>();
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().GlobalEnvironment;

        Assert.IsTrue(handler.TryProcessArgument(["port:int=2222"], environment));
        Assert.IsTrue(environment.TryResolveVariable("port", out int value));
        Assert.AreEqual(2222, value);
    });

    [TestMethod]
    public Task Test_TryProcessArgument_InvalidTypedValue_ReturnsFalseAsync() => TestWithDIAsync(services =>
    {
        IEnvironmentVariableArgumentHandler handler = services.GetRequiredService<IEnvironmentVariableArgumentHandler>();
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().GlobalEnvironment;

        Assert.IsFalse(handler.TryProcessArgument(["port:int=not-json"], environment));
        Assert.IsFalse(environment.TryResolveVariable("port", out int _));
    });

    [TestMethod]
    public Task Test_TryProcessArgument_CollectionAppend_DefinesVirtualCollectionAsync() => TestWithDIAsync(services =>
    {
        IEnvironmentVariableArgumentHandler handler = services.GetRequiredService<IEnvironmentVariableArgumentHandler>();
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().GlobalEnvironment;

        Assert.IsTrue(handler.TryProcessArgument(["items[]+=alpha", "items[]+=beta"], environment));
        Assert.IsFalse(handler.TryProcessArgument(["items[+]=nope"], environment));

        Assert.IsTrue(environment.TryResolveVariable("items[]", out IEnumerable<object>? items));
        Assert.IsNotNull(items);
        Assert.AreSequenceEqual(new object[] { "alpha", "beta" }, items);
    });
}
