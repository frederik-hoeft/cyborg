using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using System.Text.Json;

namespace Cyborg.Core.Tests.Syntax;

[TestClass]
public sealed class VariableSyntaxBuilderTests
{
    [TestMethod]
    public void Path_Root_ReturnsEmptyPath()
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        PathSyntax syntax = builder.Root();

        Assert.AreEqual(string.Empty, syntax.ToString());
        Assert.IsTrue(syntax.IsEmpty);
    }

    [TestMethod]
    public void Path_SingleSegment_ReturnsSegment()
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        string syntax = builder.Path("backup_hosts");

        Assert.AreEqual("backup_hosts", syntax);
    }

    [TestMethod]
    public void Path_Member_UsesNamingPolicy()
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        string syntax = builder.Path("backup_hosts").Member("WakeOnLanMac");

        Assert.AreEqual("backup_hosts.wake_on_lan_mac", syntax);
    }

    [TestMethod]
    public void Self_RendersExpectedSymbol()
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        string syntax = builder.Self();

        Assert.AreEqual("@", syntax);
    }

    private static VariableSyntaxBuilder CreateBuilder() => new(JsonNamingPolicy.SnakeCaseLower);
}
