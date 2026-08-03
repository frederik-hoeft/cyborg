using Cyborg.Core.Modules.Runtime;
using Cyborg.Modules.Empty;

namespace Cyborg.Modules.Tests.Smoke;

/// <summary>
/// Smoke tests that verify the test infrastructure itself works correctly.
/// </summary>
[TestClass]
public sealed class CoreSmokeTests : ModuleTestBase
{
    private const string EMPTY_MODULE_JSON =
        """
        { "cyborg.modules.empty.v1": { } }
        """;

    [TestMethod]
    public Task TestDeserializationAsync_EmptyModule_DeserializesCorrectlyAsync() =>
        TestDeserializationAsync<EmptyModule>(EMPTY_MODULE_JSON, module =>
            MSAssert.AreEqual("cyborg.modules.empty.v1", EmptyModule.ModuleId));

    [TestMethod]
    public Task TestValidationAsync_EmptyModule_ProducesValidResultAsync() =>
        TestValidationAsync<EmptyModule>(EMPTY_MODULE_JSON, result =>
            MSAssert.IsTrue(result.IsValid));

    [TestMethod]
    public Task TestModuleAsync_EmptyModule_WorkerAndModuleAreAvailableAsync() =>
        TestModuleAsync<EmptyModule, EmptyModuleWorker>(EMPTY_MODULE_JSON, (module, worker, result) =>
        {
            MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
            MSAssert.IsNotNull(module);
            MSAssert.IsNotNull(worker);
        });

    [TestMethod]
    public Task TestExecutionAsync_EmptyModule_ReturnsSuccessAsync() =>
        TestExecutionAsync(EMPTY_MODULE_JSON, result =>
            MSAssert.AreEqual(ModuleExitStatus.Success, result.Status));
}
