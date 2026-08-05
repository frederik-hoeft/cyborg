using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime.Environments;
using System.Text.Json;

namespace Cyborg.Core.Tests.Runtime;

[TestClass]
public sealed class EnvironmentInterpolationTests
{
    [TestMethod]
    [DataRow("${#HOME}", "${HOME}")]
    [DataRow("${##HOME}", "${#HOME}")]
    [DataRow("before ${#HOME} after", "before ${HOME} after")]
    [DataRow("${#}", "${}")]
    public void Test_Interpolate_HashLiteral_StripsExactlyOneHash(string value, string expected)
    {
        GlobalRuntimeEnvironment environment = CreateEnvironment();

        string actual = environment.Interpolate(value);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Test_Interpolate_OrdinaryAndEscapedExpressions_ResolvesThenFinalizes()
    {
        GlobalRuntimeEnvironment environment = CreateEnvironment();
        environment.SetVariable("prefix", "resolved");

        string actual = environment.Interpolate("${prefix}/${#HOME}");

        Assert.AreEqual("resolved/${HOME}", actual);
    }

    [TestMethod]
    public void Test_Interpolate_RevealedExpression_IsNotRescanned()
    {
        GlobalRuntimeEnvironment environment = CreateEnvironment();
        environment.SetVariable("HOME", "resolved-home");

        string actual = environment.Interpolate("${#HOME}");

        Assert.AreEqual("${HOME}", actual);
    }

    [TestMethod]
    public void Test_Interpolate_DoubleEscapeAcrossExplicitCalls_StripsOneHashPerCall()
    {
        GlobalRuntimeEnvironment environment = CreateEnvironment();
        environment.SetVariable("HOME", "resolved-home");

        string firstPass = environment.Interpolate("${##HOME}");
        string secondPass = environment.Interpolate(firstPass);
        string thirdPass = environment.Interpolate(secondPass);

        Assert.AreEqual("${#HOME}", firstPass);
        Assert.AreEqual("${HOME}", secondPass);
        Assert.AreEqual("resolved-home", thirdPass);
    }

    [TestMethod]
    public void Test_TryResolveVariable_EscapedExpressionIntroducedByVariable_FinalizesAtExplicitBoundary()
    {
        GlobalRuntimeEnvironment environment = CreateEnvironment();
        environment.SetVariable("shell_expression", "${#HOME}");
        environment.SetVariable("template", "${shell_expression}");

        bool resolved = environment.TryResolveVariable("template", out string? actual);

        Assert.IsTrue(resolved);
        Assert.AreEqual("${HOME}", actual);
    }

    [TestMethod]
    public void Test_TryResolveVariable_ForwardReference_UsesValueAtReadTime()
    {
        GlobalRuntimeEnvironment environment = CreateEnvironment();
        environment.SetVariable("template", "${value}");
        environment.SetVariable("value", "first");
        environment.SetVariable("value", "second");

        bool resolved = environment.TryResolveVariable("template", out string? actual);

        Assert.IsTrue(resolved);
        Assert.AreEqual("second", actual);
    }

    [TestMethod]
    public void Test_TryResolveVariable_LateReference_UsesOriginalEntryPoint()
    {
        GlobalRuntimeEnvironment parent = CreateEnvironment();
        parent.SetVariable("template", "${@value}");
        parent.SetVariable("value", "parent");
        InheritedRuntimeEnvironment child = new(
            Name: "child",
            Parent: parent,
            IsTransient: false,
            SyntaxFactory: parent.SyntaxFactory,
            Namespace: string.Empty);
        child.SetVariable("value", "child");

        bool resolved = child.TryResolveVariable("template", out string? actual);

        Assert.IsTrue(resolved);
        Assert.AreEqual("child", actual);
    }

    [TestMethod]
    public void Test_TryResolveVariable_CurrentAndEntryPointSelfReferences_UseDistinctScopes()
    {
        GlobalRuntimeEnvironment parent = CreateEnvironment() with
        {
            Namespace = "parent",
        };
        parent.SetVariable("template", "${@}/${@@}");
        InheritedRuntimeEnvironment child = new(
            Name: "child",
            Parent: parent,
            IsTransient: false,
            SyntaxFactory: parent.SyntaxFactory,
            Namespace: "child");

        bool resolved = child.TryResolveVariable("template", out string? actual);

        Assert.IsTrue(resolved);
        Assert.AreEqual("parent/child", actual);
    }

    [TestMethod]
    public void Test_Interpolate_UnresolvedExpression_RemainsUnchanged()
    {
        GlobalRuntimeEnvironment environment = CreateEnvironment();

        string actual = environment.Interpolate("before ${missing} after");

        Assert.AreEqual("before ${missing} after", actual);
    }

    [TestMethod]
    public void Test_TryResolveVariable_CyclicReference_ThrowsInvalidOperationException()
    {
        GlobalRuntimeEnvironment environment = CreateEnvironment();
        environment.SetVariable("first", "${second}");
        environment.SetVariable("second", "${first}");

        Assert.ThrowsExactly<InvalidOperationException>(() => environment.TryResolveVariable("first", out string? _));
    }

    [TestMethod]
    public void Test_Resolve_StringFallback_PerformsCompleteEvaluation()
    {
        GlobalRuntimeEnvironment environment = CreateEnvironment();
        ProbeModule module = new(Value: "${value}/${#HOME}", Port: 0);
        environment.SetVariable("value", "resolved");

        string? actual = environment.Resolve(module, module.Value);

        Assert.AreEqual("resolved/${HOME}", actual);
    }

    [TestMethod]
    public void Test_Resolve_StringOverride_PerformsCompleteEvaluation()
    {
        GlobalRuntimeEnvironment environment = CreateEnvironment();
        ProbeModule module = new(Value: "fallback", Port: 0) { Name = "probe" };
        environment.SetVariable("@probe.value", "${#HOME}");

        string? actual = environment.Resolve(module, module.Value);

        Assert.AreEqual("${HOME}", actual);
    }

    [TestMethod]
    public void Test_Resolve_NonStringOverride_PreservesTypedIndirection()
    {
        GlobalRuntimeEnvironment environment = CreateEnvironment();
        ProbeModule module = new(Value: null, Port: 0) { Name = "probe" };
        environment.SetVariable("port", 22);
        environment.SetVariable("@probe.port", "${port}");

        int actual = environment.Resolve(module, module.Port);

        Assert.AreEqual(22, actual);
    }

    [TestMethod]
    public void Test_PublicEnvironmentSurface_DoesNotExposeGeneratedPreparationOperations()
    {
        string[] methodNames = typeof(IRuntimeEnvironment).GetMethods().Select(static method => method.Name).ToArray();

        CollectionAssert.DoesNotContain(methodNames, "InterpolateFinal");
        CollectionAssert.DoesNotContain(methodNames, "SelectStringOverride");
        CollectionAssert.DoesNotContain(methodNames, "SelectRawStringOverride");
        CollectionAssert.DoesNotContain(methodNames, "ResolveCollection");
    }

    private static GlobalRuntimeEnvironment CreateEnvironment() => new(JsonNamingPolicy.SnakeCaseLower);

    private sealed record ProbeModule(string? Value, int Port) : ModuleBase, IModule
    {
        public static string ModuleId => "cyborg.tests.interpolation-probe.v1";
    }
}
