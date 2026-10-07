using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using System.Text.Json;

namespace Cyborg.Core.Tests.Syntax;

[TestClass]
public sealed class TypedValueSyntaxTests
{
    [TestMethod]
    public void Test_Indirect_RendersEntireValue()
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        string syntax = builder.Path("host").Indirect();

        Assert.AreEqual("&{host}", syntax);
    }

    [TestMethod]
    public void Test_Indirect_Member_StaysInsideDelimiters()
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        string syntax = builder.Path("host").Member("Port").Indirect();

        Assert.AreEqual("&{host.port}", syntax);
    }

    [TestMethod]
    public void Test_LateIndirect_PrefixesEntryPointInsideDelimiters()
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        string syntax = builder.Path("host").Child(builder.Path("port")).LateIndirect();

        Assert.AreEqual("&{@host.port}", syntax);
    }

    [TestMethod]
    public void Test_Indirect_FromSelf_RendersCurrentNamespaceReference()
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        Assert.AreEqual("&{@}", builder.Self().Indirect().ToString());
        Assert.AreEqual("&{@@}", builder.Self().LateIndirect().ToString());
    }

    [TestMethod]
    public void Test_Capture_RendersIdentifier()
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        string syntax = builder.Path("host").Child(builder.Path("port")).Capture();

        Assert.AreEqual("*{host.port}", syntax);
    }

    [TestMethod]
    public void Test_Shield_InsertsOneHashPerOperatorBrace()
    {
        Assert.AreEqual("hello ${#name}", InterpolationShield.Shield("hello ${name}"));
        Assert.AreEqual("${##name}", InterpolationShield.Shield("${#name}"));
        Assert.AreEqual("&{#port}", InterpolationShield.Shield("&{port}"));
        Assert.AreEqual("*{#port}", InterpolationShield.Shield("*{port}"));
        Assert.AreEqual("plain", InterpolationShield.Shield("plain"));
    }

    private static VariableSyntaxBuilder CreateBuilder() => new(JsonNamingPolicy.SnakeCaseLower);
}
