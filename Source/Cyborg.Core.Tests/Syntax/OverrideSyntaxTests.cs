using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using System.Text.Json;

namespace Cyborg.Core.Tests.Syntax;

[TestClass]
public sealed class OverrideSyntaxTests
{
    [TestMethod]
    public void Override_Member_AppendsSegment()
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        string syntax = builder.Path("if_condition").Override().Member("Result");

        Assert.AreEqual("@if_condition.result", syntax);
    }

    [TestMethod]
    public void Override_ChildPath_AppendsSegment()
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        string syntax = builder.Path("if_condition").Override().Child(builder.Path("result"));

        Assert.AreEqual("@if_condition.result", syntax);
    }

    private static VariableSyntaxBuilder CreateBuilder() => new(JsonNamingPolicy.SnakeCaseLower);
}
