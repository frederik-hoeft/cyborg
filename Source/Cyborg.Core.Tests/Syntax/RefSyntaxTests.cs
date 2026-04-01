using System.Text.Json;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;

namespace Cyborg.Core.Tests.Syntax;

[TestClass]
public sealed class RefSyntaxTests
{
    [TestMethod]
    public void Ref_FromSelf_RendersExpectedReference()
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        RefSyntax syntax = builder.Self().Ref();

        Assert.AreEqual("${@}", syntax.ToString());
    }

    [TestMethod]
    public void Ref_Member_AppendsOutsideInterpolationDelimiters()
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        string syntax = builder.Self().Ref().Member("Result");

        Assert.AreEqual("${@}.result", syntax);
    }

    [TestMethod]
    public void Ref_ChildPath_AppendsOutsideInterpolationDelimiters()
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        string syntax = builder.Path("host").Ref().Child(builder.Path("port"));

        Assert.AreEqual("${host}.port", syntax);
    }

    [TestMethod]
    public void Ref_Property_UsesNamingPolicy()
    {
        VariableSyntaxBuilder builder = CreateBuilder();
        TestModel model = new(ResultValue: true);

        string syntax = builder.Self().Ref().Property(model.ResultValue);

        Assert.AreEqual("${@}.result_value", syntax);
    }

    private static VariableSyntaxBuilder CreateBuilder() => new(JsonNamingPolicy.SnakeCaseLower);

    private sealed record TestModel(bool ResultValue);
}
