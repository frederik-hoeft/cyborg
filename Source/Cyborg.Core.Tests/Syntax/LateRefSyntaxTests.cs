using System.Text.Json;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;

namespace Cyborg.Core.Tests.Syntax;

[TestClass]
public sealed class LateRefSyntaxTests
{
    [TestMethod]
    public void LateRef_FromSelf_RendersExpectedReference()
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        LateRefSyntax syntax = builder.Self().LateRef();

        Assert.AreEqual("${@@}", syntax.ToString());
    }

    [TestMethod]
    public void LateRef_Member_AppendsOutsideInterpolationDelimiters()
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        string syntax = builder.Self().LateRef().Member("Result");

        Assert.AreEqual("${@@}.result", syntax);
    }

    [TestMethod]
    public void LateRef_ChildPath_AppendsOutsideInterpolationDelimiters()
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        string syntax = builder.Path("host").LateRef().Child(builder.Path("port"));

        Assert.AreEqual("${@host}.port", syntax);
    }

    private static VariableSyntaxBuilder CreateBuilder() => new(JsonNamingPolicy.SnakeCaseLower);
}
