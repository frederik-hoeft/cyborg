using Cyborg.Modules.Configuration.ConfigMap;
using Cyborg.Modules.Template;
using DynamicKvp = Cyborg.Core.Configuration.Model.DynamicKeyValuePair;

namespace Cyborg.Modules.Tests.Core.AutoInterpolation;

[TestClass]
public sealed class DynamicKeyValuePairValidationTests : ModuleTestBase
{
    private string? _tempFile;

    [TestInitialize]
    public void Setup() => _tempFile = Path.GetTempFileName();

    [TestCleanup]
    public void Cleanup()
    {
        if (_tempFile is not null && File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }

    [TestMethod]
    public Task Test_ConfigMap_KeyInterpolatesFromEnvironmentVariable() => TestOverridesAsync<ConfigMapModule>(
        """
        {
            "cyborg.modules.config.map.v1": {
            "entries": [
                { "key": "${key_name}", "string": "value" }
            ]
            }
        }
        """,
        env => env.SetVariable("key_name", "my_resolved_key"),
        module =>
        {
            MSAssert.HasCount(1, module.Entries);
            MSAssert.AreEqual("my_resolved_key", module.Entries.First().Key);
        });

    [TestMethod]
    public Task Test_ConfigMap_LiteralKeyPassesThrough() => TestOverridesAsync<ConfigMapModule>(
        """
        {
            "cyborg.modules.config.map.v1": {
            "entries": [
                { "key": "literal_key", "string": "value" }
            ]
            }
        }
        """,
        static _ => { },
        module => MSAssert.AreEqual("literal_key", module.Entries.First().Key));

    [TestMethod]
    public Task Test_ConfigMap_MultipleEntriesAllKeysInterpolated() => TestOverridesAsync<ConfigMapModule>(
        """
        {
            "cyborg.modules.config.map.v1": {
            "entries": [
                { "key": "${k1}", "string": "v1" },
                { "key": "${k2}", "string": "v2" }
            ]
            }
        }
        """,
        env =>
        {
            env.SetVariable("k1", "first_key");
            env.SetVariable("k2", "second_key");
        },
        module =>
        {
            List<DynamicKvp> entries = [.. module.Entries];
            MSAssert.AreEqual("first_key", entries[0].Key);
            MSAssert.AreEqual("second_key", entries[1].Key);
        });

    [TestMethod]
    public Task Test_ConfigMap_MissingKeyProducesValidationError() => TestValidationAsync<ConfigMapModule>(
        """
        {
            "cyborg.modules.config.map.v1": {
            "entries": [
                { "key": "", "string": "value" }
            ]
            }
        }
        """,
        result =>
        {
            MSAssert.IsFalse(result.IsValid);
            MSAssert.Contains(e => e.Rule == "required" && e.PropertyName.EndsWith(nameof(DynamicKvp.Key), StringComparison.Ordinal), result.Errors);
        });

    [TestMethod]
    public Task Test_Template_ArgumentKeyInterpolatesFromEnvironmentVariable() => TestOverridesAsync<TemplateModule>(
        $$"""
        {
            "cyborg.modules.template.v1": {
            "namespace": "test_ns",
            "path": "{{_tempFile!.Replace("\\", "\\\\")}}",
            "arguments": [
                { "key": "${arg_key}", "string": "arg_value" }
            ]
            }
        }
        """,
        env => env.SetVariable("arg_key", "my_arg"),
        module =>
        {
            MSAssert.HasCount(1, module.Arguments);
            MSAssert.AreEqual("my_arg", module.Arguments.First().Key);
        });

    [TestMethod]
    public Task Test_Template_OverrideKeyInterpolatesFromEnvironmentVariable() => TestOverridesAsync<TemplateModule>(
        $$"""
        {
            "cyborg.modules.template.v1": {
            "namespace": "test_ns",
            "path": "{{_tempFile!.Replace("\\", "\\\\")}}",
            "overrides": [
                { "key": "@${override_name}", "string": "override_value" }
            ]
            }
        }
        """,
        env => env.SetVariable("override_name", "my_setting"),
        module =>
        {
            MSAssert.HasCount(1, module.Overrides);
            MSAssert.AreEqual("@my_setting", module.Overrides.First().Key);
        });
}
