using Cyborg.Core.Modules.Debugging.Breakpoints;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class BreakpointRegistryTests : CyborgCoreTestBase
{
    [TestMethod]
    public void Test_Add_And_List_ReturnsRegisteredBreakpoints()
    {
        BreakpointRegistry registry = new();
        int id1 = registry.Add("foo");
        int id2 = registry.Add("bar");

        IReadOnlyList<BreakpointExpression> list = registry.ToList();
        Assert.HasCount(2, list);
        Assert.AreEqual(id1, list[0].Id);
        Assert.AreEqual("foo", list[0].Expression);
        Assert.AreEqual(id2, list[1].Id);
    }

    [TestMethod]
    public void Test_TryMatchAndConsume_MatchesModuleId()
    {
        BreakpointRegistry registry = new();
        registry.Add("cyborg\\.modules\\.empty\\.v1");

        BreakpointContext context = new("cyborg.modules.empty.v1", Name: null, Group: null);
        bool matched = registry.TryMatchAndConsume(in context, out BreakpointExpression? bp);
        Assert.IsTrue(matched);
        Assert.IsNotNull(bp);
        Assert.AreEqual(1, registry.Count); // persistent breakpoint remains
    }

    [TestMethod]
    public void Test_TryMatchAndConsume_MatchesNameAndGroup()
    {
        BreakpointRegistry registry = new();
        registry.Add("^my-step$");

        BreakpointContext nameContext = new("cyborg.modules.empty.v1", Name: "my-step", Group: null);
        BreakpointContext groupContext = new("cyborg.modules.empty.v1", Name: null, Group: "my-step");
        BreakpointContext otherContext = new("cyborg.modules.empty.v1", Name: "other", Group: "other");
        Assert.IsTrue(registry.TryMatchAndConsume(in nameContext, out _));
        Assert.IsTrue(registry.TryMatchAndConsume(in groupContext, out _));
        Assert.IsFalse(registry.TryMatchAndConsume(in otherContext, out _));
    }

    [TestMethod]
    public void Test_TryMatchAndConsume_RemovesOneShotBreakpoint()
    {
        BreakpointRegistry registry = new();
        registry.Add(".*", isOneShot: true);

        BreakpointContext context = new("anything", Name: null, Group: null);
        Assert.IsTrue(registry.TryMatchAndConsume(in context, out BreakpointExpression? bp));
        Assert.IsTrue(bp!.IsOneShot);
        Assert.AreEqual(0, registry.Count);
        Assert.IsFalse(registry.TryMatchAndConsume(in context, out _));
    }

    [TestMethod]
    public void Test_Remove_ById_Works()
    {
        BreakpointRegistry registry = new();
        int id = registry.Add("x");
        Assert.IsTrue(registry.Remove(id));
        Assert.AreEqual(0, registry.Count);
        Assert.IsFalse(registry.Remove(id));
    }

    [TestMethod]
    public void Test_Clear_RemovesAll()
    {
        BreakpointRegistry registry = new();
        registry.Add("a");
        registry.Add("b");
        registry.Clear();
        Assert.AreEqual(0, registry.Count);
    }
}
