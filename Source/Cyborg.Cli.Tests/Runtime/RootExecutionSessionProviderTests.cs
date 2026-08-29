using Cyborg.Core.Runtime.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Cli.Tests.Runtime;

[TestClass]
public sealed class RootExecutionSessionProviderTests
{
    [TestMethod]
    public void ResolveRuntime_GeneratedProviderCreatesIndependentRootSessions()
    {
        using DefaultServiceProvider services = new();

        IModuleRuntime first = services.GetRequiredService<IModuleRuntime>();
        IModuleRuntime second = services.GetRequiredService<IModuleRuntime>();

        Assert.AreNotSame(first, second);
        Assert.AreNotSame(first.GlobalEnvironment, second.GlobalEnvironment);
        first.GlobalEnvironment.SetVariable("session", "first");
        Assert.IsFalse(second.GlobalEnvironment.TryResolveVariable("session", out string? _));
    }
}
