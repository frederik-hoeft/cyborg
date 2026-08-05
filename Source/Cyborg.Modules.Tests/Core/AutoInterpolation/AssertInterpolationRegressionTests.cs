using Cyborg.Core.Modules.Runtime;
using Cyborg.Modules.Assert;

namespace Cyborg.Modules.Tests.Core.AutoInterpolation;

[TestClass]
public sealed class AssertInterpolationRegressionTests : ModuleTestBase
{
    private const string ASSERT_MODULE_JSON =
        """
        {
          "cyborg.modules.assert.v1": {
            "name": "assertion",
            "assertion": {
              "cyborg.modules.condition.is_true.v1": {
                "variable": "condition"
              }
            },
            "message": "Condition result: ${assertion.result}"
          }
        }
        """;

    [TestMethod]
    public Task TestModuleAsync_MessageIsInterpolatedAfterAssertionArtifactsArePublishedAsync() =>
        TestModuleAsync<AssertModule, AssertModuleWorker>(
            ASSERT_MODULE_JSON,
            (_, _, result) =>
            {
                MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status);
                MSAssert.IsTrue(result.Artifacts.TryResolveVariable("assertion.message", out string? message));
                MSAssert.AreEqual("Condition result: False", message);
            },
            environmentSetup: environment =>
            {
                environment.SetVariable("condition", false);
                environment.SetVariable("assertion.result", true);
            });
}
