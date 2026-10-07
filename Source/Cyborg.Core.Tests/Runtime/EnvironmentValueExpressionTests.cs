using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Cyborg.Core.Tests.Runtime;

[TestClass]
public sealed class EnvironmentValueExpressionTests : CyborgCoreTestBase
{
    [TestMethod]
    public Task Test_ExactInterpolation_StringifiesNonStringTargetAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = Environment(services);
        environment.SetVariable("port", 22);
        environment.SetVariable("label", "${port}");

        Assert.IsTrue(environment.TryResolveVariable("label", out string? text));
        Assert.AreEqual("22", text);
        Assert.ThrowsExactly<InvalidCastException>(() => environment.TryResolveVariable("label", out int _));
    });

    [TestMethod]
    public Task Test_LazyIndirection_PreservesTypeAndTracksLatestValueAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = Environment(services);
        environment.SetVariable("port", 22);
        environment.SetVariable("alias", "&{port}");
        environment.SetVariable("port", 23);

        Assert.IsTrue(environment.TryResolveVariable("alias", out int value));
        Assert.AreEqual(23, value);
    });

    [TestMethod]
    public Task Test_LazyIndirection_EntryPointModifier_UsesOriginalScopeAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = Environment(services);
        environment.SetVariable("alias", "&{@port}");
        environment.SetVariable("port", 22);
        InheritedRuntimeEnvironment child = new(
            Name: "child",
            Parent: environment,
            IsTransient: false,
            SyntaxFactory: environment.SyntaxFactory,
            Namespace: "child");
        child.SetVariable("port", 80);

        Assert.IsTrue(child.TryResolveVariable("alias", out int value));
        Assert.AreEqual(80, value);
    });

    [TestMethod]
    public Task Test_LazyIndirection_SelfReferences_ResolveNamespacesAsync() => TestWithDIAsync(_ =>
    {
        GlobalRuntimeEnvironment environment = new(JsonNamingPolicy.SnakeCaseLower)
        {
            Namespace = "parent"
        };
        environment.SetVariable("current_ns", "&{@}");
        environment.SetVariable("entry_ns", "&{@@}");
        InheritedRuntimeEnvironment child = new(
            Name: "child",
            Parent: environment,
            IsTransient: false,
            SyntaxFactory: environment.SyntaxFactory,
            Namespace: "child");

        Assert.IsTrue(child.TryResolveVariable("current_ns", out string? current));
        Assert.IsTrue(child.TryResolveVariable("entry_ns", out string? entry));
        Assert.AreEqual("parent", current);
        Assert.AreEqual("child", entry);
    });

    [TestMethod]
    public Task Test_LazyIndirection_UndefinedTarget_ThrowsAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = Environment(services);
        environment.SetVariable("alias", "&{missing}");

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => environment.TryResolveVariable("alias", out object? _));

        StringAssert.Contains(exception.Message, "missing");
    });

    [TestMethod]
    public Task Test_LazyIndirection_EmbeddedInText_IsRejectedAtDefinitionAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = Environment(services);

        Assert.ThrowsExactly<FormatException>(() => environment.SetVariable("bad", "port=&{port}"));
        Assert.ThrowsExactly<FormatException>(() => environment.Interpolate("port=&{port}"));
        Assert.ThrowsExactly<FormatException>(() => environment.Interpolate("&{port}"));
    });

    [TestMethod]
    public Task Test_IndirectionEscape_FinalizesToLiteralAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = Environment(services);
        environment.SetVariable("port", 22);
        environment.SetVariable("literal", "&{#port}");

        Assert.IsTrue(environment.TryResolveVariable("literal", out string? value));
        Assert.AreEqual("&{port}", value);
    });

    [TestMethod]
    public Task Test_EagerCapture_SnapshotsValueAndIgnoresLaterReplacementAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = Environment(services);
        environment.SetVariable("port", 22);
        environment.SetVariable("snapshot", "*{port}");
        environment.SetVariable("port", 23);

        Assert.IsTrue(environment.TryResolveVariable("snapshot", out int value));
        Assert.AreEqual(22, value);
        Assert.IsTrue(environment.Where(static pair => pair.Key == "snapshot").Select(static pair => pair.Value).Single() is int stored && stored == 22);
    });

    [TestMethod]
    public Task Test_EagerCapture_CopiesReferenceWithoutCloningAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = Environment(services);
        List<int> items = [1];
        environment.SetVariable("items", items);
        environment.SetVariable("snapshot", "*{items}");
        items.Add(2);

        Assert.IsTrue(environment.TryResolveVariable("snapshot", out List<int>? snapshot));
        Assert.AreSame(items, snapshot);
        Assert.HasCount(2, snapshot);
    });

    [TestMethod]
    public Task Test_EagerCapture_UndefinedTarget_ThrowsAndDoesNotStoreAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = Environment(services);

        Assert.ThrowsExactly<InvalidOperationException>(() => environment.SetVariable("snapshot", "*{missing}"));
        Assert.IsFalse(environment.TryResolveVariable("snapshot", out object? _));
    });

    [TestMethod]
    public Task Test_EagerCapture_EntryPointModifier_IsReservedAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = Environment(services);
        environment.SetVariable("port", 22);

        Assert.ThrowsExactly<FormatException>(() => environment.SetVariable("snapshot", "*{@port}"));
        Assert.ThrowsExactly<FormatException>(() => environment.SetVariable("snapshot", "*{@}"));
        Assert.ThrowsExactly<FormatException>(() => environment.Interpolate("*{port}"));
    });

    [TestMethod]
    public Task Test_CaptureEscape_IsNotEvaluatedAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = Environment(services);
        environment.SetVariable("port", 22);
        environment.SetVariable("literal", "*{#port}");

        Assert.IsTrue(environment.TryResolveVariable("literal", out string? value));
        Assert.AreEqual("*{port}", value);
    });

    [TestMethod]
    public Task Test_EagerCapture_TextSnapshot_StaysLiteralAcrossReadsAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = Environment(services);
        environment.SetVariable("template", "${#name}");
        environment.SetVariable("name", "resolved");
        environment.SetVariable("snapshot", "*{template}");

        Assert.IsTrue(environment.TryResolveVariable("snapshot", out string? snapshot));
        Assert.AreEqual("${name}", snapshot);
        Assert.AreEqual("x ${name} y", environment.Interpolate("x ${snapshot} y").Value);
    });

    [TestMethod]
    public Task Test_EagerCapture_TaggedWrapper_UnionsTagsOntoTextualTargetAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = Environment(services);
        environment.SetVariable("secret", new TaggedString("s3cret", [WellKnownTags.SECRET]));
        environment.SetVariable("snapshot", new TaggedString("*{secret}", ["wrapper"]));

        Assert.IsTrue(environment.TryResolveVariable("snapshot", out TaggedString tagged));
        Assert.AreEqual("s3cret", tagged.Value);
        Assert.IsTrue(tagged.HasTag(WellKnownTags.SECRET));
        Assert.IsTrue(tagged.HasTag("wrapper"));
    });

    [TestMethod]
    public Task Test_SetResolvedVariable_PreservesFinalizedIndirectionTextAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = Environment(services);
        environment.SetVariable("literal", "&{#port}");
        Assert.IsTrue(environment.TryResolveVariable("literal", out string? finalized));
        Assert.AreEqual("&{port}", finalized);
        Assert.IsInstanceOfType<EnvironmentLike>(environment);
        ((EnvironmentLike)environment).SetResolvedVariable("copied", finalized);
        environment.SetVariable("port", 22);

        Assert.IsTrue(environment.TryResolveVariable("copied", out string? copied));
        Assert.AreEqual("&{port}", copied);
    });

    [TestMethod]
    public Task Test_KeyInterpolation_StillRejectsTypedFormsAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = Environment(services);
        environment.SetVariable("module", "probe");

        Assert.AreEqual("@probe.property", environment.Interpolate("@${module}.property").Value);
        Assert.ThrowsExactly<FormatException>(() => environment.Interpolate("@&{module}.property"));
        Assert.ThrowsExactly<FormatException>(() => environment.Interpolate("@*{module}.property"));
    });

    private static IRuntimeEnvironment Environment(IServiceProvider services) => services.GetRequiredService<IModuleRuntime>().Environment;
}
