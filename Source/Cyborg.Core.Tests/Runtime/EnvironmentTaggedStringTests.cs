using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
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
        environment.SetVariable("secret", new TaggedString("s3cret", [WellKnownTags.SECRET]));

        TaggedString actual = environment.Interpolate("hello ${secret}");

        Assert.AreEqual("hello s3cret", actual.Value);
        Assert.IsTrue(actual.HasTag(WellKnownTags.SECRET));
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
        environment.SetVariable("token", new TaggedString("abc", [WellKnownTags.SECRET]));

        Assert.IsTrue(environment.TryResolveVariable("token", out TaggedString tagged));
        Assert.AreEqual("abc", tagged.Value);
        Assert.IsTrue(tagged.HasTag(WellKnownTags.SECRET));
    });

    [TestMethod]
    public Task Test_TryResolveVariable_String_StillReturnsRawValueAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().Environment;
        environment.SetVariable("token", new TaggedString("abc", [WellKnownTags.SECRET]));

        Assert.IsTrue(environment.TryResolveVariable("token", out string? raw));
        Assert.AreEqual("abc", raw);
    });

    [TestMethod]
    public Task Test_TryResolveVariable_String_UsesConversionObserverFromDIAsync()
    {
        RecordingTaggedStringConversionObserver observer = new();
        return TestWithDIAsync(services =>
        {
            IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().Environment;
            TaggedString tagged = new("abc", [WellKnownTags.SECRET]);
            environment.SetVariable("token", tagged);

            Assert.IsTrue(environment.TryResolveVariable("token", out string? raw));
            Assert.AreEqual("abc", raw);
            Assert.AreEqual("token", observer.VariableName);
            Assert.AreEqual(tagged, observer.Value);
        }, services => services.AddSingleton<ITaggedStringConversionObserver>(observer));
    }

    [TestMethod]
    public Task Test_RuntimeEnvironment_UsesVariableSyntaxBuilderFromDIAsync() => TestWithDIAsync(services =>
    {
        VariableSyntaxBuilder syntaxFactory = services.GetRequiredService<VariableSyntaxBuilder>();
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().Environment;

        Assert.AreSame(syntaxFactory, environment.SyntaxFactory);
    });

    [TestMethod]
    public Task Test_ModuleArtifacts_UseRuntimeServicesFromDIAsync()
    {
        RecordingTaggedStringConversionObserver observer = new();
        return TestWithDIAsync(services =>
        {
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            IModuleArtifactsFactory artifactsFactory = services.GetRequiredService<IModuleArtifactsFactory>();
            VariableSyntaxBuilder syntaxFactory = services.GetRequiredService<VariableSyntaxBuilder>();
            ProbeModule module = new() { Artifacts = ModuleArtifacts.Default with { Namespace = "probe" } };
            IModuleArtifactsBuilder artifacts = artifactsFactory.CreateArtifacts(runtime, module);
            TaggedString tagged = new("abc", [WellKnownTags.SECRET]);

            Assert.AreSame(syntaxFactory, artifacts.SyntaxFactory);
            artifacts.Expose("token", tagged);
            IEnvironmentLike artifactEnvironment = artifacts.Build(ModuleExitStatus.Success);
            Assert.IsTrue(artifactEnvironment.TryResolveVariable("token", out string? raw));
            Assert.AreEqual("abc", raw);
            Assert.AreEqual("token", observer.VariableName);
            Assert.AreEqual(tagged, observer.Value);
        }, services => services.AddSingleton<ITaggedStringConversionObserver>(observer));
    }

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
        environment.SetVariable("secret", new TaggedString("s3cret", [WellKnownTags.SECRET]));
        environment.SetVariable("greeting", "hello ${secret}");

        Assert.IsTrue(environment.TryResolveVariable("greeting", out TaggedString tagged));
        Assert.AreEqual("hello s3cret", tagged.Value);
        Assert.IsTrue(tagged.HasTag(WellKnownTags.SECRET));
    });

    [TestMethod]
    public Task Test_Indirection_UnionsWrapperAndTargetTagsAsync() => TestWithDIAsync(services =>
    {
        IRuntimeEnvironment environment = services.GetRequiredService<IModuleRuntime>().Environment;
        environment.SetVariable("secret", new TaggedString("s3cret", [WellKnownTags.SECRET]));
        environment.SetVariable("alias", new TaggedString("${secret}", ["wrapper"]));

        Assert.IsTrue(environment.TryResolveVariable("alias", out TaggedString tagged));
        Assert.AreEqual("s3cret", tagged.Value);
        Assert.IsTrue(tagged.HasTag(WellKnownTags.SECRET));
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

    private sealed record ProbeModule : ModuleBase, IModule;

    private sealed class RecordingTaggedStringConversionObserver : ITaggedStringConversionObserver
    {
        public string? VariableName { get; private set; }

        public TaggedString? Value { get; private set; }

        public void OnImplicitStringRetrieval(string variableName, TaggedString value)
        {
            VariableName = variableName;
            Value = value;
        }
    }
}
