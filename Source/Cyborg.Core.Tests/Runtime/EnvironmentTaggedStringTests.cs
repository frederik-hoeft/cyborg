using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Text;
using Cyborg.Core.Text.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Tests.Runtime;

[TestClass]
public sealed class EnvironmentTaggedStringTests : CyborgCoreTestBase
{
    [TestMethod]
    public Task Test_Interpolate_UnionsTagsFromReferencedVariablesAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().Environment;
        environment.SetVariable("secret", new TaggedString("s3cret", [WellKnownTags.Secret]));

        TaggedString actual = environment.Interpolate("hello ${secret}");

        Assert.AreEqual("hello s3cret", actual.Value);
        Assert.IsTrue(actual.HasTag(WellKnownTags.Secret));
        Assert.AreEqual(SecretTagHandler.RedactedDisplay, actual.ToString());
    });

    [TestMethod]
    public Task Test_Interpolate_UnionsTagsFromMultipleVariablesAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().Environment;
        environment.SetVariable("left", new TaggedString("A", ["alpha"]));
        environment.SetVariable("right", new TaggedString("B", ["beta"]));

        TaggedString actual = environment.Interpolate("${left}-${right}");

        Assert.AreEqual("A-B", actual.Value);
        Assert.IsTrue(actual.HasTag("alpha"));
        Assert.IsTrue(actual.HasTag("beta"));
    });

    [TestMethod]
    public Task Test_TryResolveVariable_TaggedString_PreservesTagsAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().Environment;
        environment.SetVariable("token", new TaggedString("abc", [WellKnownTags.Secret]));

        Assert.IsTrue(environment.TryResolveVariable("token", out TaggedString tagged));
        Assert.AreEqual("abc", tagged.Value);
        Assert.IsTrue(tagged.HasTag(WellKnownTags.Secret));
    });

    [TestMethod]
    public Task Test_TryResolveVariable_String_StillReturnsRawValueAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().Environment;
        environment.SetVariable("token", new TaggedString("abc", [WellKnownTags.Secret]));

        Assert.IsTrue(environment.TryResolveVariable("token", out string? raw));
        Assert.AreEqual("abc", raw);
    });

    [TestMethod]
    public Task Test_TryResolveVariable_StringVariableAsTaggedString_IsUntaggedAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().Environment;
        environment.SetVariable("plain", "hello");

        Assert.IsTrue(environment.TryResolveVariable("plain", out TaggedString tagged));
        Assert.AreEqual("hello", tagged.Value);
        Assert.IsFalse(tagged.HasTags);
    });

    [TestMethod]
    public Task Test_TryResolveVariable_InterpolationIntoStoredString_PromotesTagsAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().Environment;
        environment.SetVariable("secret", new TaggedString("s3cret", [WellKnownTags.Secret]));
        environment.SetVariable("greeting", "hello ${secret}");

        Assert.IsTrue(environment.TryResolveVariable("greeting", out TaggedString tagged));
        Assert.AreEqual("hello s3cret", tagged.Value);
        Assert.IsTrue(tagged.HasTag(WellKnownTags.Secret));
    });

    [TestMethod]
    public Task Test_Indirection_UnionsWrapperAndTargetTagsAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().Environment;
        environment.SetVariable("secret", new TaggedString("s3cret", [WellKnownTags.Secret]));
        environment.SetVariable("alias", new TaggedString("${secret}", ["wrapper"]));

        Assert.IsTrue(environment.TryResolveVariable("alias", out TaggedString tagged));
        Assert.AreEqual("s3cret", tagged.Value);
        Assert.IsTrue(tagged.HasTag(WellKnownTags.Secret));
        Assert.IsTrue(tagged.HasTag("wrapper"));
    });

    [TestMethod]
    public Task Test_Interpolate_PreservesTemplateTagsAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().Environment;
        environment.SetVariable("name", "world");

        TaggedString actual = environment.Interpolate(new TaggedString("hello ${name}", ["template"]));

        Assert.AreEqual("hello world", actual.Value);
        Assert.IsTrue(actual.HasTag("template"));
    });
}
