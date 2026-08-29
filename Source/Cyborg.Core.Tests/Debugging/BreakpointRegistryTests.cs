using Cyborg.Core.Runtime.Services.Debugging;
using Cyborg.Core.Runtime.Services.Debugging.Breakpoints;

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
    public void Test_EvaluateAndConsume_MatchesModuleId()
    {
        BreakpointRegistry registry = new();
        registry.Add("cyborg\\.modules\\.empty\\.v1");

        BreakpointContext context = new("cyborg.modules.empty.v1", Name: null, Group: null);
        BreakpointEvaluationResult result = registry.EvaluateAndConsume(in context);

        Assert.AreEqual(BreakpointEvaluationStatus.Match, result.Status);
        Assert.IsNotNull(result.Breakpoint);
        Assert.AreEqual(1, registry.Count); // persistent breakpoint remains
    }

    [TestMethod]
    public void Test_EvaluateAndConsume_MatchesNameAndGroup()
    {
        BreakpointRegistry registry = new();
        registry.Add("^my-step$");

        BreakpointContext nameContext = new("cyborg.modules.empty.v1", Name: "my-step", Group: null);
        BreakpointContext groupContext = new("cyborg.modules.empty.v1", Name: null, Group: "my-step");
        BreakpointContext otherContext = new("cyborg.modules.empty.v1", Name: "other", Group: "other");
        Assert.AreEqual(BreakpointEvaluationStatus.Match, registry.EvaluateAndConsume(in nameContext).Status);
        Assert.AreEqual(BreakpointEvaluationStatus.Match, registry.EvaluateAndConsume(in groupContext).Status);
        Assert.AreEqual(BreakpointEvaluationStatus.NoMatch, registry.EvaluateAndConsume(in otherContext).Status);
    }

    [TestMethod]
    public void Test_EvaluateAndConsume_RemovesOneShotBreakpoint()
    {
        BreakpointRegistry registry = new();
        registry.Add(".*", isOneShot: true);

        BreakpointContext context = new("anything", Name: null, Group: null);
        BreakpointEvaluationResult result = registry.EvaluateAndConsume(in context);

        Assert.AreEqual(BreakpointEvaluationStatus.Match, result.Status);
        Assert.IsNotNull(result.Breakpoint);
        Assert.IsTrue(result.Breakpoint.IsOneShot);
        Assert.AreEqual(0, registry.Count);
        Assert.AreEqual(BreakpointEvaluationStatus.NoMatch, registry.EvaluateAndConsume(in context).Status);
    }

    [TestMethod]
    public void Test_EvaluateAndConsume_OneShotBreakpointTakesPriorityOverOlderPersistentMatch()
    {
        BreakpointRegistry registry = new();
        int persistentId = registry.Add(".*");
        int oneShotId = registry.Add(".*", isOneShot: true);

        BreakpointContext context = new("anything", Name: null, Group: null);
        BreakpointEvaluationResult result = registry.EvaluateAndConsume(in context);

        Assert.AreEqual(BreakpointEvaluationStatus.Match, result.Status);
        Assert.IsNotNull(result.Breakpoint);
        Assert.AreEqual(oneShotId, result.Breakpoint.Id);
        Assert.IsTrue(result.Breakpoint.IsOneShot);
        IReadOnlyList<BreakpointExpression> remaining = registry.ToList();
        Assert.HasCount(1, remaining);
        Assert.AreEqual(persistentId, remaining[0].Id);
    }

    [TestMethod]
    public void Test_EvaluateAndConsume_NewerOneShotBreakpointIsEvaluatedFirst()
    {
        BreakpointRegistry registry = new();
        int olderOneShotId = registry.Add(".*", isOneShot: true);
        int newerOneShotId = registry.Add(".*", isOneShot: true);

        BreakpointEvaluationResult result = registry.EvaluateAndConsume(["anything"]);

        Assert.AreEqual(BreakpointEvaluationStatus.Match, result.Status);
        Assert.IsNotNull(result.Breakpoint);
        Assert.AreEqual(newerOneShotId, result.Breakpoint.Id);
        Assert.Contains(breakpoint => breakpoint.Id == olderOneShotId, registry.ToList());
    }

    [TestMethod]
    public void Test_EvaluateAndConsume_RegexTimeoutPausesWithDiagnostic()
    {
        BreakpointRegistry registry = new(TimeSpan.FromMilliseconds(1));
        int id = registry.Add("^(a+)+$");
        string target = $"{new string('a', 100_000)}!";

        BreakpointEvaluationResult result = registry.EvaluateAndConsume([target]);

        Assert.AreEqual(BreakpointEvaluationStatus.Faulted, result.Status);
        Assert.IsTrue(result.ShouldPause);
        Assert.IsNotNull(result.Breakpoint);
        Assert.AreEqual(id, result.Breakpoint.Id);
        Assert.HasCount(1, result.Diagnostics);
        Assert.AreEqual(DebugDiagnosticSeverity.Error, result.Diagnostics[0].Severity);
        Assert.IsTrue(result.Diagnostics[0].Message.Contains("regex match timeout", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(1, registry.Count);
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
