using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Text;
using System.Text.Json;

namespace Cyborg.Core.Tests.Runtime;

/// <summary>
/// Characterizes override selection through structural environment parents independently of
/// module invocation ancestry and transactional environment registration.
/// </summary>
[TestClass]
public sealed class InheritedEnvironmentResolutionTests
{
    [TestMethod]
    public void TypedOverride_NearestMatchingViewWinsAcrossMultipleParents()
    {
        GlobalRuntimeEnvironment root = CreateRoot();
        InheritedRuntimeEnvironment middle = Inherit(root, "middle");
        InheritedRuntimeEnvironment child = Inherit(middle, "child");
        ProbeModule module = new(Port: 0, Value: null, Ports: null) { Name = "probe" };
        root.SetVariable("@probe.port", 80);
        middle.SetVariable("@probe.port", 40);
        child.SetVariable("@probe.port", 20);

        Assert.AreEqual(20, child.Resolve(module, module.Port));
        child.TryRemoveVariable("@probe.port");
        Assert.AreEqual(40, child.Resolve(module, module.Port));
        middle.TryRemoveVariable("@probe.port");
        Assert.AreEqual(80, child.Resolve(module, module.Port));
    }

    [TestMethod]
    public void OverrideIdentifiers_PrecedeParentDepthBecauseEachCandidateSearchesInheritedStores()
    {
        GlobalRuntimeEnvironment root = CreateRoot();
        InheritedRuntimeEnvironment child = Inherit(root, "child");
        ProbeModule module = new(Port: 0, Value: null, Ports: null) { Name = "probe", Group = "group" };
        root.SetVariable("@probe.port", 80);
        child.SetVariable("@group.port", 40);

        // The child checks the name candidate recursively before its own group candidate.
        Assert.AreEqual(80, child.Resolve(module, module.Port));
    }

    [TestMethod]
    public void TypedOverride_ParentExpressionKeepsOriginalEntryPoint()
    {
        GlobalRuntimeEnvironment root = CreateRoot() with { Namespace = "root" };
        InheritedRuntimeEnvironment middle = Inherit(root, "middle");
        InheritedRuntimeEnvironment child = Inherit(middle, "child");
        ProbeModule module = new(Port: 0, Value: "configured", Ports: null) { Name = "probe" };
        root.SetVariable("@probe.value", "${@}/${@@}");
        root.SetVariable("@probe.port", "&{@port}");
        child.SetVariable("port", 42);

        Assert.AreEqual("root/child", child.Resolve(module, module.Value));
        Assert.AreEqual(42, child.Resolve(module, module.Port));
    }

    [TestMethod]
    public void RawTaggedOverride_FromAncestorRetainsTemplateAndTags()
    {
        GlobalRuntimeEnvironment root = CreateRoot();
        InheritedRuntimeEnvironment middle = Inherit(root, "middle");
        InheritedRuntimeEnvironment child = Inherit(middle, "child");
        ProbeModule module = new(Port: 0, Value: null, Ports: null) { Name = "probe" };
        root.SetVariable("value", "resolved");
        root.SetVariable("@probe.value", new TaggedString("before ${value}", ["secret"]));

        TaggedString actual = ((IRuntimeEnvironment)child).SelectRawTaggedStringOverride(module, new TaggedString("configured"), nameof(module), "module.Value", shieldInterpolation: false);

        Assert.AreEqual("before ${value}", actual.Value);
        Assert.IsTrue(actual.Tags.Contains("secret"));
    }

    [TestMethod]
    public void TypedOverride_CapturedTextDoesNotInterpolateAgain()
    {
        GlobalRuntimeEnvironment root = CreateRoot();
        InheritedRuntimeEnvironment child = Inherit(root, "child");
        ProbeModule module = new(Port: 0, Value: "configured", Ports: null) { Name = "probe" };
        root.SetVariable("HOME", "would-be-replaced");
        root.SetVariable("source", "${#HOME}");
        root.SetVariable("@probe.value", "*{source}");

        Assert.AreEqual("${HOME}", child.Resolve(module, module.Value));
    }

    [TestMethod]
    public void CollectionOverride_AncestorSelectionPreservesOriginalCollectionReference()
    {
        GlobalRuntimeEnvironment root = CreateRoot();
        InheritedRuntimeEnvironment middle = Inherit(root, "middle");
        InheritedRuntimeEnvironment child = Inherit(middle, "child");
        IReadOnlyCollection<int> ancestorPorts = [80, 443];
        IReadOnlyCollection<int> middlePorts = [8080];
        ProbeModule module = new(Port: 0, Value: null, Ports: [22]) { Name = "probe" };
        root.SetVariable("@probe.ports", ancestorPorts);

        IReadOnlyCollection<int>? fromRoot = ((IRuntimeEnvironment)child).ResolveCollection(module, module.Ports, nameof(module), "module.Ports");
        Assert.AreSame(ancestorPorts, fromRoot);

        middle.SetVariable("@probe.ports", middlePorts);
        IReadOnlyCollection<int>? fromMiddle = ((IRuntimeEnvironment)child).ResolveCollection(module, module.Ports, nameof(module), "module.Ports");
        Assert.AreSame(middlePorts, fromMiddle);
    }

    [TestMethod]
    public void RawStringOverride_CapturedValueShieldsInterpolationOnlyWhenRequested()
    {
        GlobalRuntimeEnvironment root = CreateRoot();
        InheritedRuntimeEnvironment child = Inherit(root, "child");
        ProbeModule module = new(Port: 0, Value: null, Ports: null) { Name = "probe" };
        root.SetVariable("HOME", "resolved");
        root.SetVariable("source", "${#HOME}");
        root.SetVariable("@probe.value", "*{source}");

        string? raw = ((IRuntimeEnvironment)child).SelectRawStringOverride(module, "fallback", nameof(module), "module.Value", shieldInterpolation: false);
        string? shielded = ((IRuntimeEnvironment)child).SelectRawStringOverride(module, "fallback", nameof(module), "module.Value", shieldInterpolation: true);

        Assert.AreEqual("${HOME}", raw);
        Assert.AreEqual("${#HOME}", shielded);
    }

    [TestMethod]
    public void OverrideTags_AreSelectedFromEachStructuralView()
    {
        GlobalRuntimeEnvironment root = CreateRoot() with { OverrideResolutionTags = ["root_tag"] };
        InheritedRuntimeEnvironment middle = Inherit(root, "middle") with { OverrideResolutionTags = ["middle_tag"] };
        InheritedRuntimeEnvironment child = Inherit(middle, "child") with { OverrideResolutionTags = ["child_tag"] };
        ProbeModule module = new(Port: 0, Value: null, Ports: null);
        root.SetVariable("@root_tag.port", 80);
        middle.SetVariable("@middle_tag.port", 40);
        child.SetVariable("@child_tag.port", 20);

        Assert.AreEqual(20, child.Resolve(module, module.Port));
        child.TryRemoveVariable("@child_tag.port");
        Assert.AreEqual(40, child.Resolve(module, module.Port));
        middle.TryRemoveVariable("@middle_tag.port");
        Assert.AreEqual(80, child.Resolve(module, module.Port));
    }

    private static GlobalRuntimeEnvironment CreateRoot() => new(JsonNamingPolicy.SnakeCaseLower);

    private static InheritedRuntimeEnvironment Inherit(IRuntimeEnvironment parent, string name) =>
        new(name, parent, IsTransient: false, parent.SyntaxFactory, Namespace: name);

    private sealed record ProbeModule(int Port, string? Value, IReadOnlyCollection<int>? Ports) : ModuleBase, IModuleDefinition
    {
        public static string ModuleId => "cyborg.tests.inherited-resolution.v1";
    }
}
