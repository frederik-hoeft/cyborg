using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Model;
using Cyborg.Modules.Glob;

namespace Cyborg.Modules.Tests.Runtime;

/// <summary>Tests the origin-aware execution and validation helpers against the same contextual expressions as production.</summary>
[TestClass]
public sealed class ModuleProvenanceTestAdapterTests : ModuleTestBase
{
    private static readonly ModuleOrigin s_origin = new(Path.Combine(Path.GetTempPath(), "cyborg-test-adapter.jconf"));

    private const string GLOB_JSON = """
        {
          "cyborg.modules.glob.v1": {
            "pattern": "^never-match-cyborg$",
            "root": "${@.origin.directory}"
          }
        }
        """;

    protected override ModuleOrigin? ModuleOrigin => s_origin;

    [TestMethod]
    public Task TestValidationAsync_PreservesOriginDuringGeneratedValidationAsync() =>
        TestValidationAsync<GlobModule>(GLOB_JSON, result =>
        {
            result.EnsureValid();
            MSAssert.AreEqual(s_origin.Directory, result.Module.Root);
        });

    [TestMethod]
    public Task TestValidatedModuleAsync_ExposesOriginOnRuntimeViewAsync() =>
        TestValidatedModuleAsync<GlobModule>(GLOB_JSON, (module, scope) =>
        {
            MSAssert.AreEqual(s_origin.Directory, module.Root);
            MSAssert.IsTrue(scope.Runtime.Environment.TryResolveVariable("@.origin.file", out string? file));
            MSAssert.AreEqual(s_origin.File, file);
            MSAssert.IsFalse(scope.GlobalEnvironment.TryResolveVariable("@.origin.file", out string? _));
            return Task.CompletedTask;
        });

    [TestMethod]
    public Task TestOverridesAsync_ResolvesOriginInSelectedOverrideAsync() =>
        TestOverridesAsync<GlobModule>(
            GLOB_JSON,
            environment => environment.SetVariable("@cyborg.modules.glob.v1.root", "${@.origin.directory}"),
            module => MSAssert.AreEqual(s_origin.Directory, module.Root));

    [TestMethod]
    public Task TestModuleAsync_InspectedWorkerRetainsReferenceOriginAsync() =>
        TestModuleAsync<GlobModule, GlobModuleWorker>(GLOB_JSON, (module, worker, result) =>
        {
            MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
            MSAssert.IsNotNull(worker);
            MSAssert.AreEqual("${@.origin.directory}", module.Root);
        });

    [TestMethod]
    public Task TestExecutionAsync_ReferenceExecutionRetainsOriginAsync() =>
        TestExecutionAsync(GLOB_JSON, result => MSAssert.AreEqual(ModuleExitStatus.Success, result.Status));
}
