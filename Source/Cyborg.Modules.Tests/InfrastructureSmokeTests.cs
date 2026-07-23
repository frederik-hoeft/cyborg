using Cyborg.Core.Modules.Runtime;
using Cyborg.Modules.Empty;
using Cyborg.Modules.Tests.Infrastructure;

namespace Cyborg.Modules.Tests;

/// <summary>
/// Smoke tests that verify the test infrastructure itself works correctly.
/// </summary>
[TestClass]
public sealed class InfrastructureSmokeTests : ModuleTestBase
{
    private const string EMPTY_MODULE_JSON =
        """
        { "cyborg.modules.empty.v1": { } }
        """;

    [TestMethod]
    public async Task TestDeserializationAsync_EmptyModule_DeserializesCorrectlyAsync()
    {
        await TestDeserializationAsync<EmptyModule>(
            EMPTY_MODULE_JSON,
            async module => 
            {
                MSAssert.AreEqual("cyborg.modules.empty.v1", EmptyModule.ModuleId);
            });
    }

    [TestMethod]
    public async Task TestExecutionAsync_EmptyModule_ReturnsSuccessAsync()
    {
        await TestExecutionAsync(
            EMPTY_MODULE_JSON,
            result => MSAssert.AreEqual(ModuleExitStatus.Success, result.Status));
    }

    [TestMethod]
    public async Task TestValidationAsync_EmptyModule_ProducesValidResultAsync()
    {
        await TestValidationAsync<EmptyModule>(
            EMPTY_MODULE_JSON,
            result => MSAssert.IsTrue(result.IsValid));
    }

    [TestMethod]
    public async Task TestModuleAsync_EmptyModule_WorkerAndModuleAreAvailableAsync()
    {
        await TestModuleAsync<EmptyModule, EmptyModuleWorker>(
            EMPTY_MODULE_JSON,
            (module, worker, result) =>
            {
                MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
                MSAssert.IsNotNull(module);
                MSAssert.IsNotNull(worker);
            });
    }
}
