using Cyborg.Modules.Network.WakeOnLan;

namespace Cyborg.Modules.Tests.Network;

[TestClass]
public sealed class WakeOnLanValueExpressionTests : ModuleTestBase
{
    private string? _tempExecutable;

    [TestInitialize]
    public void Setup() => _tempExecutable = Path.GetTempFileName();

    [TestCleanup]
    public void Cleanup()
    {
        if (_tempExecutable is not null && File.Exists(_tempExecutable))
        {
            File.Delete(_tempExecutable);
        }
    }

    [TestMethod]
    public Task Test_LivenessProbePort_LazyIndirection_UsesLatestIntegerAsync() =>
        TestOverridesAsync<WakeOnLanModule>(ModuleJson(), environment =>
        {
            environment.SetVariable("port", 22);
            environment.SetVariable("@wol.liveness_probe_port", "&{port}");
            environment.SetVariable("port", 2222);
        }, module => MSAssert.AreEqual(2222, module.LivenessProbePort));

    [TestMethod]
    public Task Test_LivenessProbePort_EagerCapture_KeepsIntegerFromDefinitionAsync() =>
        TestOverridesAsync<WakeOnLanModule>(ModuleJson(), environment =>
        {
            environment.SetVariable("port", 22);
            environment.SetVariable("@wol.liveness_probe_port", "*{port}");
            environment.SetVariable("port", 2222);
        }, module => MSAssert.AreEqual(22, module.LivenessProbePort));

    [TestMethod]
    public Task Test_LivenessProbePort_UndefinedIndirection_FailsResolutionAsync() =>
        MSAssert.ThrowsExactlyAsync<InvalidOperationException>(() => TestOverridesAsync<WakeOnLanModule>(
            ModuleJson(),
            environment => environment.SetVariable("@wol.liveness_probe_port", "&{missing}"),
            static module => MSAssert.Fail(module.LivenessProbePort.ToString())));

    private string ModuleJson() => $$"""
        {
          "cyborg.modules.network.wol.v1": {
            "name": "wol",
            "target_host": "backup",
            "mac_address": "00:11:22:33:44:55",
            "liveness_probe_port": 1,
            "executable": "{{_tempExecutable!.Replace("\\", "\\\\")}}"
          }
        }
        """;
}
