using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Environments.Context;
using Cyborg.Core.Runtime.Model;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Cyborg.Core.Tests.Runtime;

[TestClass]
public sealed class EnvironmentOriginTests
{
    [TestMethod]
    public void Test_FromFile_NormalizesToAbsoluteFileAndDirectory()
    {
        string relative = Path.Combine("workflows", "job.jconf");

        ModuleOrigin origin = ModuleOrigin.FromFile(relative);

        string expectedFile = Path.GetFullPath(relative);
        Assert.AreEqual(expectedFile, origin.File);
        Assert.AreEqual(Path.GetDirectoryName(expectedFile), origin.Directory);
    }

    [TestMethod]
    public void Test_OriginMembers_ResolveForCurrentScopeAndEntryPoint()
    {
        ModuleOrigin parentOrigin = ModuleOrigin.FromFile(Path.Combine("parent-dir", "parent.jconf"));
        ModuleOrigin childOrigin = ModuleOrigin.FromFile(Path.Combine("child-dir", "child.jconf"));
        GlobalRuntimeEnvironment parent = CreateEnvironment("parent", parentOrigin);
        parent.SetVariable("owned_by_parent", "${@.origin.file}|${@@.origin.file}");
        parent.SetVariable("parent_directory", "&{@.origin.directory}");
        InheritedRuntimeEnvironment child = new(
            Name: "child",
            Parent: parent,
            IsTransient: true,
            SyntaxFactory: parent.SyntaxFactory,
            Namespace: "child")
        {
            Context = EnvironmentContext.FromOrigin(childOrigin)
        };

        Assert.IsTrue(child.TryResolveVariable("@.origin.file", out string? childFile));
        Assert.IsTrue(child.TryResolveVariable("@.origin.directory", out string? childDirectory));
        Assert.AreEqual(childOrigin.File, childFile);
        Assert.AreEqual(childOrigin.Directory, childDirectory);
        Assert.AreEqual($"{childOrigin.File}|{childOrigin.File}", child.Interpolate("${@.origin.file}|${@@.origin.file}").Value);
        Assert.IsTrue(child.TryResolveVariable("owned_by_parent", out string? owned));
        Assert.AreEqual($"{parentOrigin.File}|{childOrigin.File}", owned);
        Assert.IsTrue(child.TryResolveVariable("parent_directory", out string? parentDirectory));
        Assert.AreEqual(parentOrigin.Directory, parentDirectory);
    }

    [TestMethod]
    public void Test_MissingOrigin_IsUndefinedAndDoesNotFallBackToParent()
    {
        ModuleOrigin parentOrigin = ModuleOrigin.FromFile(Path.Combine("parent-dir", "parent.jconf"));
        GlobalRuntimeEnvironment parent = CreateEnvironment("parent", parentOrigin);
        InheritedRuntimeEnvironment child = new(
            Name: "child",
            Parent: parent,
            IsTransient: true,
            SyntaxFactory: parent.SyntaxFactory,
            Namespace: "child");

        Assert.IsFalse(child.TryResolveVariable("@.origin.file", out string? _));
        Assert.IsFalse(child.TryResolveVariable("@.origin.directory", out string? _));
        Assert.AreEqual("${@.origin.file}", child.Interpolate("${@.origin.file}").Value);
        child.SetVariable("alias", "&{@.origin.file}");
        Assert.ThrowsExactly<InvalidOperationException>(() => child.TryResolveVariable("alias", out object? _));
        Assert.AreEqual(parentOrigin.File, parent.Interpolate("${@.origin.file}").Value);
    }

    [TestMethod]
    public void Test_EagerCapture_SnapshotsDefinitionOrigin()
    {
        ModuleOrigin origin = ModuleOrigin.FromFile(Path.Combine("jobs", "daily.jconf"));
        GlobalRuntimeEnvironment environment = CreateEnvironment("daily", origin);

        environment.SetVariable("file_now", "*{@.origin.file}");
        environment.SetVariable("directory_now", "*{@@.origin.directory}");

        Assert.IsTrue(environment.TryResolveVariable("file_now", out string? file));
        Assert.IsTrue(environment.TryResolveVariable("directory_now", out string? directory));
        Assert.AreEqual(origin.File, file);
        Assert.AreEqual(origin.Directory, directory);
        Assert.IsFalse(environment.Any(static pair => pair.Key.StartsWith("@.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Test_ContextualNames_AreReadOnlyAndAbsentFromStorage()
    {
        GlobalRuntimeEnvironment environment = CreateEnvironment("job", ModuleOrigin.FromFile(Path.Combine("jobs", "job.jconf")));

        Assert.ThrowsExactly<InvalidOperationException>(() => environment.SetVariable("@.origin.file", "shadow"));
        Assert.ThrowsExactly<InvalidOperationException>(() => environment.TryRemoveVariable("@.origin.directory"));
        Assert.ThrowsExactly<InvalidOperationException>(() => environment.SetVariable("@.future.value", "reserved"));
        Assert.IsFalse(environment.Any(static pair => pair.Key.StartsWith("@.", StringComparison.Ordinal)));
        environment.SetVariable("@job.path", "override");
        Assert.IsTrue(environment.TryResolveVariable("@job.path", out string? stored));
        Assert.AreEqual("override", stored);
    }

    [TestMethod]
    public void Test_Compose_FirstContributorWinsAndEmptyContributorsAreSkipped()
    {
        IEnvironmentContext origin = EnvironmentContext.FromOrigin(ModuleOrigin.FromFile(Path.Combine("jobs", "job.jconf")));
        IEnvironmentContext extra = new StaticContext("future.slot", "later");
        IEnvironmentContext replacement = new StaticContext(OriginEnvironmentContext.FilePath, "replaced");

        IEnvironmentContext composed = EnvironmentContext.Compose([EnvironmentContext.Empty, origin, extra]);
        IEnvironmentContext overridden = EnvironmentContext.Compose([replacement, origin]);

        Assert.IsTrue(composed.TryGetValue(OriginEnvironmentContext.FilePath, out object? file));
        Assert.AreEqual(ModuleOrigin.FromFile(Path.Combine("jobs", "job.jconf")).File, file);
        Assert.IsTrue(composed.TryGetValue("future.slot", out object? slot));
        Assert.AreEqual("later", slot);
        Assert.IsTrue(overridden.TryGetValue(OriginEnvironmentContext.FilePath, out object? replaced));
        Assert.AreEqual("replaced", replaced);
        Assert.IsTrue(overridden.TryGetValue(OriginEnvironmentContext.DirectoryPath, out object? directory));
        Assert.AreEqual(ModuleOrigin.FromFile(Path.Combine("jobs", "job.jconf")).Directory, directory);
    }

    [TestMethod]
    public void Test_OriginScope_RestoresPreviousOrigin()
    {
        ModuleOrigin outer = ModuleOrigin.FromFile(Path.Combine("outer", "outer.jconf"));
        ModuleOrigin inner = ModuleOrigin.FromFile(Path.Combine("inner", "inner.jconf"));

        Assert.IsNull(ModuleOriginScope.Current);
        using (ModuleOriginScope.Push(outer))
        {
            Assert.AreEqual(outer, ModuleOriginScope.Current);
            using (ModuleOriginScope.Push(inner))
            {
                Assert.AreEqual(inner, ModuleOriginScope.Current);
            }
            Assert.AreEqual(outer, ModuleOriginScope.Current);
        }
        Assert.IsNull(ModuleOriginScope.Current);
    }

    private static GlobalRuntimeEnvironment CreateEnvironment(string ns, ModuleOrigin origin) => new(JsonNamingPolicy.SnakeCaseLower)
    {
        Namespace = ns,
        Context = EnvironmentContext.FromOrigin(origin)
    };

    private sealed class StaticContext(string path, string value) : IEnvironmentContext
    {
        public bool TryGetValue(string candidate, [NotNullWhen(true)] out object? resolved)
        {
            if (candidate.Equals(path, StringComparison.Ordinal))
            {
                resolved = value;
                return true;
            }
            resolved = null;
            return false;
        }
    }
}
