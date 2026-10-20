using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Expressions;
using Cyborg.Core.Runtime.Engine.Expressions.Bound;
using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Runtime.Services.Validation.Internal;
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
    public void DefinedNull_ShadowsInheritedVariableUntilRemoved()
    {
        GlobalRuntimeEnvironment root = CreateRoot();
        InheritedRuntimeEnvironment child = Inherit(root, "child");
        root.SetVariable("present", "parent");
        child.SetVariable<object?>("present", null);
        child.SetVariable("alias", "&{present}");

        Assert.IsTrue(child.TryResolveVariable("present", out string? current));
        Assert.IsNull(current);
        Assert.IsTrue(child.TryResolveVariable("alias", out object? alias));
        Assert.IsNull(alias);
        Assert.IsTrue(child.TryRemoveVariable("present"));
        Assert.IsTrue(child.TryResolveVariable("present", out string? inherited));
        Assert.AreEqual("parent", inherited);
    }

    [TestMethod]
    public void PresentNullOverride_ShadowsLowerPriorityIdentifiersAndInheritedValues()
    {
        GlobalRuntimeEnvironment root = CreateRoot();
        InheritedRuntimeEnvironment child = Inherit(root, "child");
        ProbeModule module = new(Port: 22, Value: "configured", Ports: [22]) { Name = "probe", Group = "group" };
        root.SetVariable<object?>("@probe.value", null);
        child.SetVariable("@group.value", "lower-priority");
        root.SetVariable("@group.value", "root-group");
        child.SetVariable("@probe.port", 40);
        child.SetVariable<IReadOnlyCollection<int>?>("@probe.ports", null);
        child.SetVariable("@group.ports", (IReadOnlyCollection<int>)[80]);

        Assert.IsNull(child.Resolve(module, module.Value));
        Assert.IsNull(((IRuntimeEnvironment)child).ResolveCollection(module, module.Ports, nameof(module), "module.Ports"));
        Assert.AreEqual(40, child.Resolve(module, module.Port));
        Assert.IsTrue(child.TryRemoveVariable("@probe.ports"));
        Assert.AreEqual(80, ((IRuntimeEnvironment)child).ResolveCollection(module, module.Ports, nameof(module), "module.Ports")!.Single());
        Assert.IsTrue(root.TryRemoveVariable("@probe.value"));
        Assert.AreEqual("lower-priority", child.Resolve(module, module.Value));
    }

    [TestMethod]
    public void NullableTypedOverride_AcceptsNullButNonNullableValueTypeRejectsIt()
    {
        GlobalRuntimeEnvironment root = CreateRoot();
        ProbeModule module = new(Port: 22, Value: "configured", Ports: null) { Name = "probe" };
        root.SetVariable<object?>("@probe.port", null);

        Assert.IsNull(root.Resolve(module, (int?)module.Port, nameof(module), "module.Port"));
        Assert.ThrowsExactly<InvalidCastException>(() => root.Resolve(module, module.Port));
    }

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
    public void StructuralOverride_FromAncestorRetainsTemplateAndTags()
    {
        GlobalRuntimeEnvironment root = CreateRoot();
        InheritedRuntimeEnvironment middle = Inherit(root, "middle");
        InheritedRuntimeEnvironment child = Inherit(middle, "child");
        ProbeModule module = new(Port: 0, Value: null, Ports: null) { Name = "probe" };
        root.SetVariable("value", "resolved");
        root.SetVariable("@probe.value", new TaggedString("before ${value}", ["secret"]));

        Assert.IsTrue(child.TrySelectBoundOverride(module, nameof(module), "module.Value", out SelectedBoundOverride? selected));
        TaggedString stored = (TaggedString)selected.Value.PresentStoredValue()!;
        Assert.AreEqual("before ${value}", stored.Value);
        Assert.IsTrue(stored.Tags.Contains("secret"));

        object? materialized = new ExpressionMaterializer().Materialize(selected.Source.EvaluateStructuralValue(selected.Value, child));
        Assert.IsInstanceOfType<TaggedString>(materialized);
        TaggedString resolved = (TaggedString)materialized;
        Assert.AreEqual("before resolved", resolved.Value);
        Assert.IsTrue(resolved.Tags.Contains("secret"));
    }

    [TestMethod]
    public void StructuralOverride_FromGrandparentResolvesInStorageViewWithChildEntryPoint()
    {
        ModuleOrigin definingOrigin = new(Path.Combine(Path.GetTempPath(), "cyborg-overrides", "a.jconf"));
        ModuleOrigin invokingOrigin = new(Path.Combine(Path.GetTempPath(), "cyborg-overrides", "b.jconf"));
        GlobalRuntimeEnvironment root = CreateRoot() with { Namespace = "root", Context = new EnvironmentContext(definingOrigin) };
        InheritedRuntimeEnvironment middle = Inherit(root, "middle") with { Context = new EnvironmentContext(invokingOrigin) };
        InheritedRuntimeEnvironment child = Inherit(middle, "child") with { Context = new EnvironmentContext(invokingOrigin) };
        ProbeModule module = new(Port: 0, Value: "configured", Ports: null) { Name = "probe", Group = "group" };
        const string expression = "${@.origin.file}|${@@.origin.file}|${@}|${@@}";
        root.SetVariable("ordinary", expression);
        root.SetVariable("@probe.value", new TaggedString(expression, ["source-tag"]));
        child.SetVariable("@group.value", "lower-priority");

        Assert.IsTrue(child.TryResolveVariable("ordinary", out string? inherited));
        string expected = $"{definingOrigin.File}|{invokingOrigin.File}|root|child";
        Assert.AreEqual(expected, inherited);

        // The first candidate is found recursively from the child even though its actual owner is the grandparent.
        Assert.IsTrue(child.TrySelectBoundOverride(module, nameof(module), "module.Value", out SelectedBoundOverride? selected));
        Assert.AreSame(root, selected.Source);

        StructuralModulePreparation preparation = new(child);
        Assert.IsTrue(preparation.TrySelectOverride(module, nameof(module), "module.Value", "module.Value", out object? raw));
        Assert.AreEqual(expression, Assert.IsInstanceOfType<TaggedString>(raw).Value);
        object? prepared = preparation.ResolveValueExpression(raw!, willInterpolate: true, "module.Value");
        TaggedString evaluated = preparation.Interpolate(prepared!, "module.Value");
        Assert.AreEqual(expected, evaluated.Value);
        Assert.IsTrue(evaluated.HasTag("source-tag"));

        StructuralModulePreparation ignored = new(child);
        Assert.IsTrue(ignored.TrySelectOverride(module, nameof(module), "module.Value", "module.Value", out object? ignoredRaw));
        object? uninterpolated = ignored.ResolveValueExpression(ignoredRaw!, willInterpolate: false, "module.Value");
        Assert.AreEqual(expression, Assert.IsInstanceOfType<TaggedString>(uninterpolated).Value);
    }

    [TestMethod]
    public void StructuralOverride_LazyIndirectionResolvesAgainstStorageView()
    {
        ModuleOrigin definingOrigin = new(Path.Combine(Path.GetTempPath(), "cyborg-overrides", "a.jconf"));
        ModuleOrigin invokingOrigin = new(Path.Combine(Path.GetTempPath(), "cyborg-overrides", "b.jconf"));
        GlobalRuntimeEnvironment root = CreateRoot() with { Context = new EnvironmentContext(definingOrigin) };
        InheritedRuntimeEnvironment child = Inherit(root, "child") with { Context = new EnvironmentContext(invokingOrigin) };
        ProbeModule module = new(Port: 0, Value: "configured", Ports: null) { Name = "probe" };
        root.SetVariable("@probe.value", "&{@.origin.file}");

        StructuralModulePreparation preparation = new(child);
        Assert.IsTrue(preparation.TrySelectOverride(module, nameof(module), "module.Value", "module.Value", out object? raw));
        object? prepared = preparation.ResolveValueExpression(raw!, willInterpolate: true, "module.Value");
        TaggedString evaluated = preparation.Interpolate(prepared!, "module.Value");
        Assert.AreEqual(definingOrigin.File, evaluated.Value);
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
    public void StructuralOverride_CapturedValueRemainsTerminalWithoutShielding()
    {
        GlobalRuntimeEnvironment root = CreateRoot();
        InheritedRuntimeEnvironment child = Inherit(root, "child");
        ProbeModule module = new(Port: 0, Value: null, Ports: null) { Name = "probe" };
        root.SetVariable("HOME", "resolved");
        root.SetVariable("source", "${#HOME}");
        root.SetVariable("@probe.value", "*{source}");

        Assert.IsTrue(child.TrySelectBoundOverride(module, nameof(module), "module.Value", out SelectedBoundOverride? selected));
        Assert.AreEqual("${HOME}", selected.Value.PresentStoredValue());
        object? materialized = new ExpressionMaterializer().Materialize(selected.Source.EvaluateStructuralValue(selected.Value, child));
        Assert.AreEqual("${HOME}", materialized);
        Assert.AreEqual("${HOME}", child.Resolve(module, module.Value));
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
