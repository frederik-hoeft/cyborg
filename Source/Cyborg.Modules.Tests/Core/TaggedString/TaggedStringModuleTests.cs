using Cyborg.Core.Modules.Descriptors;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Validation;
using Cyborg.Core.Text;
using Cyborg.Modules.Subprocess;
using Cyborg.TestModules.Secrets;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Modules.Tests.Core.Secrets;

[TestClass]
public sealed class TaggedStringModuleTests : ModuleTestBase
{
    [TestMethod]
    public Task TestValidationAsync_SecretAttribute_InjectsSecretTagAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        TaggedStringTestModule module = new(
            Plain: "visible",
            Secret: "s3cret",
            OptionalSecret: null,
            IntentionallyUntagged: "id",
            Values: ["one"]);

        IValidationResult<TaggedStringTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("visible", result.Module.Plain.Value);
        MSAssert.IsFalse(result.Module.Plain.HasTags);
        MSAssert.AreEqual("s3cret", result.Module.Secret.Value);
        MSAssert.IsTrue(result.Module.Secret.HasTag(WellKnownTags.Secret));
    });

    [TestMethod]
    public Task TestValidationAsync_InterpolationUnionsSecretIntoTaggedStringPropertyAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.Environment.SetVariable("token", new global::Cyborg.Core.Text.TaggedString("abc", [WellKnownTags.Secret]));
        TaggedStringTestModule module = new(
            Plain: "pre-${token}-post",
            Secret: "static",
            OptionalSecret: null,
            IntentionallyUntagged: "id",
            Values: default);

        IValidationResult<TaggedStringTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("pre-abc-post", result.Module.Plain.Value);
        MSAssert.IsTrue(result.Module.Plain.HasTag(WellKnownTags.Secret));
    });

    [TestMethod]
    public Task TestDeserializationAsync_StructuralTaggedStringArgument_PreservesTagsAsync() =>
        TestDeserializationAsync<SubprocessModule>(
            """
            {
              "cyborg.modules.subprocess.v1": {
                "command": {
                  "executable": "/bin/true",
                  "arguments": [
                    "plain",
                    { "value": "s3cret", "tags": ["cyborg.secret.v1"] }
                  ]
                }
              }
            }
            """,
            module =>
            {
                List<global::Cyborg.Core.Text.TaggedString> arguments = [.. module.Command.Arguments];
                MSAssert.HasCount(2, arguments);
                MSAssert.AreEqual("plain", arguments[0].Value);
                MSAssert.IsFalse(arguments[0].HasTags);
                MSAssert.AreEqual("s3cret", arguments[1].Value);
                MSAssert.IsTrue(arguments[1].HasTag(WellKnownTags.Secret));
            });

    [TestMethod]
    public Task TestModuleContextAsync_SecretDynamicValue_PublishesTaggedStringAsync() =>
        TestModuleContextAsync(
            """
            {
              "environment": { "scope": "global" },
              "module": {
                "cyborg.modules.config.map.v1": {
                  "entries": [
                    { "key": "plain", "string": "visible" },
                    { "key": "secret", "cyborg.types.secret.v1": "s3cret" },
                    {
                      "key": "tagged",
                      "cyborg.types.taggedstring.v1": { "value": "payload", "tags": ["custom"] }
                    }
                  ]
                }
              }
            }
            """,
            (result, scope) =>
            {
                MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
                IRuntimeEnvironment environment = scope.Runtime.Environment;
                MSAssert.IsTrue(environment.TryResolveVariable("plain", out global::Cyborg.Core.Text.TaggedString plain));
                MSAssert.AreEqual("visible", plain.Value);
                MSAssert.IsFalse(plain.HasTags);

                MSAssert.IsTrue(environment.TryResolveVariable("secret", out global::Cyborg.Core.Text.TaggedString secret));
                MSAssert.AreEqual("s3cret", secret.Value);
                MSAssert.IsTrue(secret.HasTag(WellKnownTags.Secret));

                MSAssert.IsTrue(environment.TryResolveVariable("tagged", out global::Cyborg.Core.Text.TaggedString tagged));
                MSAssert.AreEqual("payload", tagged.Value);
                MSAssert.IsTrue(tagged.HasTag("custom"));
                return Task.CompletedTask;
            });

    [TestMethod]
    public Task Test_ToTextAsync_SecretTaggedString_IsRedactedAsync() => TestWithDIAsync(async services =>
    {
        IModuleSerializationService serializationService = services.GetRequiredService<IModuleSerializationService>();
        TaggedStringTestModule module = new(
            Plain: "visible",
            Secret: new global::Cyborg.Core.Text.TaggedString("s3cret", [WellKnownTags.Secret]),
            OptionalSecret: null,
            IntentionallyUntagged: "id",
            Values: ["one"]);

        string text = await serializationService.ToTextAsync(module, TestContext.CancellationToken);

        MSAssert.Contains("visible", text);
        MSAssert.Contains(global::Cyborg.Core.Text.TaggedString.RedactedDisplay, text);
        MSAssert.DoesNotContain("s3cret", text);
    });
}
