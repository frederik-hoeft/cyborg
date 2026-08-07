using Cyborg.Core.Modules.Debugging.Breakpoints;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class BreakpointRegistryTests
{
    [TestMethod]
    public void Add_And_List_ReturnsRegisteredBreakpoints()
    {
        BreakpointRegistry registry = new();
        int id1 = registry.Add("foo");
        int id2 = registry.Add("bar");

        IReadOnlyList<BreakpointExpression> list = registry.List();
        Assert.HasCount(2, list);
        Assert.AreEqual(id1, list[0].Id);
        Assert.AreEqual("foo", list[0].Expression);
        Assert.AreEqual(id2, list[1].Id);
    }

    [TestMethod]
    public void TryMatchAndConsume_MatchesModuleId()
    {
        BreakpointRegistry registry = new();
        registry.Add("cyborg\\.modules\\.empty\\.v1");

        bool matched = registry.TryMatchAndConsume("cyborg.modules.empty.v1", name: null, group: null, out BreakpointExpression? bp);
        Assert.IsTrue(matched);
        Assert.IsNotNull(bp);
        Assert.AreEqual(1, registry.Count); // persistent breakpoint remains
    }

    [TestMethod]
    public void TryMatchAndConsume_MatchesNameAndGroup()
    {
        BreakpointRegistry registry = new();
        registry.Add("^my-step$");

        Assert.IsTrue(registry.TryMatchAndConsume("cyborg.modules.empty.v1", name: "my-step", group: null, out _));
        Assert.IsTrue(registry.TryMatchAndConsume("cyborg.modules.empty.v1", name: null, group: "my-step", out _));
        Assert.IsFalse(registry.TryMatchAndConsume("cyborg.modules.empty.v1", name: "other", group: "other", out _));
    }

    [TestMethod]
    public void TryMatchAndConsume_RemovesOneShotBreakpoint()
    {
        BreakpointRegistry registry = new();
        registry.Add(".*", isOneShot: true);

        Assert.IsTrue(registry.TryMatchAndConsume("anything", null, null, out BreakpointExpression? bp));
        Assert.IsTrue(bp!.IsOneShot);
        Assert.AreEqual(0, registry.Count);
        Assert.IsFalse(registry.TryMatchAndConsume("anything", null, null, out _));
    }

    [TestMethod]
    public void Remove_ById_Works()
    {
        BreakpointRegistry registry = new();
        int id = registry.Add("x");
        Assert.IsTrue(registry.Remove(id));
        Assert.AreEqual(0, registry.Count);
        Assert.IsFalse(registry.Remove(id));
    }

    [TestMethod]
    public void Clear_RemovesAll()
    {
        BreakpointRegistry registry = new();
        registry.Add("a");
        registry.Add("b");
        registry.Clear();
        Assert.AreEqual(0, registry.Count);
    }
}
