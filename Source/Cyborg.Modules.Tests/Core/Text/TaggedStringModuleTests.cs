using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Services.ModuleDescriptors;
using Cyborg.Core.Runtime.Services.Validation;
using Cyborg.Core.Text;
using Cyborg.Core.Text.Rendering;
using Cyborg.Modules.Subprocess;
using Cyborg.TestModules.Secrets;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Modules.Tests.Core.Text;

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
        MSAssert.IsTrue(result.Module.Secret.HasTag(WellKnownTags.SECRET));
    });

    [TestMethod]
    public Task TestValidationAsync_InterpolationUnionsSecretIntoTaggedStringPropertyAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.Environment.SetVariable("token", new TaggedString("abc", [WellKnownTags.SECRET]));
        TaggedStringTestModule module = new(
            Plain: "pre-${token}-post",
            Secret: "static",
            OptionalSecret: null,
            IntentionallyUntagged: "id",
            Values: default);

        IValidationResult<TaggedStringTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.AreEqual("pre-abc-post", result.Module.Plain.Value);
        MSAssert.IsTrue(result.Module.Plain.HasTag(WellKnownTags.SECRET));
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
                List<TaggedString> arguments = [.. module.Command.Arguments];
                MSAssert.HasCount(2, arguments);
                MSAssert.AreEqual("plain", arguments[0].Value);
                MSAssert.IsFalse(arguments[0].HasTags);
                MSAssert.AreEqual("s3cret", arguments[1].Value);
                MSAssert.IsTrue(arguments[1].HasTag(WellKnownTags.SECRET));
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
                MSAssert.IsTrue(environment.TryResolveVariable("plain", out TaggedString plain));
                MSAssert.AreEqual("visible", plain.Value);
                MSAssert.IsFalse(plain.HasTags);

                MSAssert.IsTrue(environment.TryResolveVariable("secret", out TaggedString secret));
                MSAssert.AreEqual("s3cret", secret.Value);
                MSAssert.IsTrue(secret.HasTag(WellKnownTags.SECRET));

                MSAssert.IsTrue(environment.TryResolveVariable("tagged", out TaggedString tagged));
                MSAssert.AreEqual("payload", tagged.Value);
                MSAssert.IsTrue(tagged.HasTag("custom"));
                return Task.CompletedTask;
            });

    [TestMethod]
    public Task TestValidationAsync_CustomTag_UsesDiRendererInErrorMessageAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        TaggedStringValidationDisplayTestModule module = new(new TaggedString("not valid", [CustomTagRenderer.Tag]));

        IValidationResult<TaggedStringValidationDisplayTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsFalse(result.IsValid);
        MSAssert.HasCount(1, result.Errors);
        ValidationError error = result.Errors[0];
        MSAssert.Contains(CustomTagRenderer.RenderedValue, error.Message);
        MSAssert.DoesNotContain("not valid", error.Message);
    }, static services => services.AddSingleton<ITaggedStringRenderer, CustomTagRenderer>());

    [TestMethod]
    public Task TestValidationAsync_SecretNullableIgnoreInterpolation_InjectsSecretTagAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        TaggedStringTestModule module = new(
            Plain: "visible",
            Secret: "secret",
            OptionalSecret: "deferred-${not-resolved}",
            IntentionallyUntagged: "id",
            Values: ["one"]);

        IValidationResult<TaggedStringTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        MSAssert.IsTrue(result.Module.OptionalSecret.HasValue);
        MSAssert.AreEqual("deferred-${not-resolved}", result.Module.OptionalSecret.Value.Value);
        MSAssert.IsTrue(result.Module.OptionalSecret.Value.HasTag(WellKnownTags.SECRET));
    });

    [TestMethod]
    public Task Test_ToTextAsync_SecretTaggedString_IsRedactedAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IModuleSerializationService serializationService = services.GetRequiredService<IModuleSerializationService>();
        TaggedStringTestModule module = new(
            Plain: "visible",
            Secret: "s3cret",
            OptionalSecret: null,
            IntentionallyUntagged: "id",
            Values: ["one"]);
        IValidationResult<TaggedStringTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        string text = await serializationService.ToTextAsync(result.Module, TestContext.CancellationToken);

        MSAssert.Contains("visible", text);
        MSAssert.Contains(SecretTagHandler.RedactedDisplay, text);
        MSAssert.DoesNotContain("s3cret", text);
    });

    [TestMethod]
    public Task Test_ToJsonAsync_SecretTaggedString_IsRedactedAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IModuleSerializationService serializationService = services.GetRequiredService<IModuleSerializationService>();
        TaggedStringTestModule module = new(
            Plain: "visible",
            Secret: "s3cret",
            OptionalSecret: null,
            IntentionallyUntagged: "id",
            Values: ["one"]);
        IValidationResult<TaggedStringTestModule> result = await module.ValidateAsync(runtime, services, TestContext.CancellationToken);

        MSAssert.IsTrue(result.IsValid);
        string json = await serializationService.ToJsonAsync(result.Module, TestContext.CancellationToken);

        MSAssert.Contains(SecretTagHandler.RedactedDisplay, json);
        MSAssert.DoesNotContain("s3cret", json);
    });

    private sealed class CustomTagRenderer : ITaggedStringRenderer
    {
        public const string Tag = "test.custom.v1";
        public const string RenderedValue = "[CUSTOM]";

        public string Render(TaggedString value) => value.HasTag(Tag) ? RenderedValue : value.Value;
    }
}
