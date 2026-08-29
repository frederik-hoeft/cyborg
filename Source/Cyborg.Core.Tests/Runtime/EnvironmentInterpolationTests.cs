using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Cyborg.Core.Tests.Runtime;

[TestClass]
public sealed class EnvironmentInterpolationTests : CyborgCoreTestBase
{
    [TestMethod]
    [DataRow("${#HOME}", "${HOME}")]
    [DataRow("${##HOME}", "${#HOME}")]
    [DataRow("before ${#HOME} after", "before ${HOME} after")]
    [DataRow("${#}", "${}")]
    public Task Test_Interpolate_HashLiteral_StripsExactlyOneHashAsync(string value, string expected) => TestWithDIAsync(services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        string actual = runtime.Environment.Interpolate(value).Value;

        Assert.AreEqual(expected, actual);
    });

    [TestMethod]
    public Task Test_Interpolate_OrdinaryAndEscapedExpressions_ResolvesThenFinalizesAsync() => TestWithDIAsync(services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IRuntimeEnvironment environment = runtime.Environment;
        environment.SetVariable("prefix", "resolved");

        string actual = environment.Interpolate("${prefix}/${#HOME}").Value;

        Assert.AreEqual("resolved/${HOME}", actual);
    });

    [TestMethod]
    public Task Test_Interpolate_RevealedExpression_IsNotRescannedAsync() => TestWithDIAsync(services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IRuntimeEnvironment environment = runtime.Environment;
        environment.SetVariable("HOME", "resolved-home");

        string actual = environment.Interpolate("${#HOME}").Value;

        Assert.AreEqual("${HOME}", actual);
    });

    [TestMethod]
    public Task Test_Interpolate_DoubleEscapeAcrossExplicitCalls_StripsOneHashPerCallAsync() => TestWithDIAsync(services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IRuntimeEnvironment environment = runtime.Environment;
        environment.SetVariable("HOME", "resolved-home");

        string firstPass = environment.Interpolate("${##HOME}").Value;
        string secondPass = environment.Interpolate(firstPass).Value;
        string thirdPass = environment.Interpolate(secondPass).Value;

        Assert.AreEqual("${#HOME}", firstPass);
        Assert.AreEqual("${HOME}", secondPass);
        Assert.AreEqual("resolved-home", thirdPass);
    });

    [TestMethod]
    public Task Test_TryResolveVariable_EscapedExpressionIntroducedByVariable_FinalizesAtExplicitBoundaryAsync() => TestWithDIAsync(services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IRuntimeEnvironment environment = runtime.Environment;
        environment.SetVariable("shell_expression", "${#HOME}");
        environment.SetVariable("template", "${shell_expression}");

        bool resolved = environment.TryResolveVariable("template", out string? actual);

        Assert.IsTrue(resolved);
        Assert.AreEqual("${HOME}", actual);
    });

    [TestMethod]
    public Task Test_TryResolveVariable_ForwardReference_UsesValueAtReadTimeAsync() => TestWithDIAsync(services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IRuntimeEnvironment environment = runtime.Environment;
        environment.SetVariable("template", "${value}");
        environment.SetVariable("value", "first");
        environment.SetVariable("value", "second");

        bool resolved = environment.TryResolveVariable("template", out string? actual);

        Assert.IsTrue(resolved);
        Assert.AreEqual("second", actual);
    });

    [TestMethod]
    public Task Test_TryResolveVariable_LateReference_UsesOriginalEntryPointAsync() => TestWithDIAsync(services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IRuntimeEnvironment environment = runtime.Environment;
        environment.SetVariable("template", "${@value}");
        environment.SetVariable("value", "parent");
        InheritedRuntimeEnvironment child = new(
            Name: "child",
            Parent: environment,
            IsTransient: false,
            SyntaxFactory: environment.SyntaxFactory,
            Namespace: string.Empty);
        child.SetVariable("value", "child");

        bool resolved = child.TryResolveVariable("template", out string? actual);

        Assert.IsTrue(resolved);
        Assert.AreEqual("child", actual);
    });

    [TestMethod]
    public Task Test_TryResolveVariable_CurrentAndEntryPointSelfReferences_UseDistinctScopesAsync() => TestWithDIAsync(services =>
    {
        GlobalRuntimeEnvironment environment = new(JsonNamingPolicy.SnakeCaseLower)
        {
            Namespace = "parent"
        };
        environment.SetVariable("template", "${@}/${@@}");
        InheritedRuntimeEnvironment child = new(
            Name: "child",
            Parent: environment,
            IsTransient: false,
            SyntaxFactory: environment.SyntaxFactory,
            Namespace: "child");

        bool resolved = child.TryResolveVariable("template", out string? actual);

        Assert.IsTrue(resolved);
        Assert.AreEqual("parent/child", actual);
    });

    [TestMethod]
    public Task Test_Interpolate_UnresolvedExpression_RemainsUnchangedAsync() => TestWithDIAsync(services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IRuntimeEnvironment environment = runtime.Environment;

        string actual = environment.Interpolate("before ${missing} after").Value;

        Assert.AreEqual("before ${missing} after", actual);
    });

    [TestMethod]
    public Task Test_TryResolveVariable_CyclicReference_ThrowsInvalidOperationExceptionAsync() => TestWithDIAsync(services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IRuntimeEnvironment environment = runtime.Environment;
        environment.SetVariable("first", "${second}");
        environment.SetVariable("second", "${first}");

        Assert.ThrowsExactly<InvalidOperationException>(() => environment.TryResolveVariable("first", out string? _));
    });

    [TestMethod]
    public Task Test_Resolve_StringFallback_PerformsCompleteEvaluationAsync() => TestWithDIAsync(services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IRuntimeEnvironment environment = runtime.Environment;
        ProbeModule module = new(Value: "${value}/${#HOME}", Port: 0);
        environment.SetVariable("value", "resolved");

        string? actual = environment.Resolve(module, module.Value);

        Assert.AreEqual("resolved/${HOME}", actual);
    });

    [TestMethod]
    public Task Test_Resolve_StringOverride_PerformsCompleteEvaluationAsync() => TestWithDIAsync(services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IRuntimeEnvironment environment = runtime.Environment;
        ProbeModule module = new(Value: "fallback", Port: 0) { Name = "probe" };
        environment.SetVariable("@probe.value", "${#HOME}");

        string? actual = environment.Resolve(module, module.Value);

        Assert.AreEqual("${HOME}", actual);
    });

    [TestMethod]
    public Task Test_Resolve_NonStringOverride_PreservesTypedIndirectionAsync() => TestWithDIAsync(services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IRuntimeEnvironment environment = runtime.Environment;
        ProbeModule module = new(Value: null, Port: 0) { Name = "probe" };
        environment.SetVariable("port", 22);
        environment.SetVariable("@probe.port", "${port}");

        int actual = environment.Resolve(module, module.Port);

        Assert.AreEqual(22, actual);
    });

    [TestMethod]
    public Task Test_Bind_SharesEnvironmentVariableStoreAsync() => TestWithDIAsync(services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IRuntimeEnvironment environment = runtime.Environment;
        environment.SetVariable("value", "before");
        IRuntimeEnvironment bound = environment.Bind("child");

        bound.SetVariable("value", "after");

        Assert.IsTrue(environment.TryResolveVariable("value", out string? parentValue));
        Assert.IsTrue(bound.TryResolveVariable("value", out string? childValue));
        Assert.AreEqual("after", parentValue);
        Assert.AreEqual("after", childValue);
    });

    [TestMethod]
    public void Test_PublicEnvironmentSurface_DoesNotExposeGeneratedPreparationOperations()
    {
        string[] methodNames = [.. typeof(IRuntimeEnvironment).GetMethods().Select(static method => method.Name)];

        Assert.DoesNotContain("InterpolateFinal", methodNames);
        Assert.DoesNotContain("SelectStringOverride", methodNames);
        Assert.DoesNotContain("SelectRawStringOverride", methodNames);
        Assert.DoesNotContain("ResolveCollection", methodNames);
    }

    private sealed record ProbeModule(string? Value, int Port) : ModuleBase, IModuleDefinition
    {
        public static string ModuleId => "cyborg.tests.interpolation-probe.v1";
    }
}
