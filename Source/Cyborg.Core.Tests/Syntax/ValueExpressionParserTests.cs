using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using System.Text.Json;

namespace Cyborg.Core.Tests.Syntax;

[TestClass]
public sealed class ValueExpressionParserTests
{
    private static VariableSyntaxBuilder CreateBuilder() => new(JsonNamingPolicy.SnakeCaseLower);

    [TestMethod]
    [DataRow("plain")]
    [DataRow("${name}")]
    [DataRow("${@name}")]
    [DataRow("${@}")]
    [DataRow("${@@}")]
    [DataRow("prefix ${name} suffix")]
    [DataRow("${#name}")]
    [DataRow("&{#name}")]
    [DataRow("*{#name}")]
    [DataRow("literal &{#name} and *{#other}")]
    public void Test_Parse_Text_DoesNotThrow(string value)
    {
        ValueExpression expression = ValueExpressionParser.Parse(CreateBuilder(), value);

        Assert.AreEqual(ValueExpressionKind.Text, expression.Kind);
    }

    [TestMethod]
    [DataRow("&{name}", "name")]
    [DataRow("&{@name}", "@name")]
    [DataRow("&{@}", "@")]
    [DataRow("&{@@}", "@@")]
    [DataRow("&{host.port}", "host.port")]
    public void Test_Parse_Indirection_ReturnsExpression(string value, string expectedExpression)
    {
        ValueExpression expression = ValueExpressionParser.Parse(CreateBuilder(), value);

        Assert.AreEqual(ValueExpressionKind.LazyIndirection, expression.Kind);
        Assert.AreEqual(expectedExpression, expression.Expression);
    }

    [TestMethod]
    [DataRow("*{name}", "name")]
    [DataRow("*{host.port}", "host.port")]
    public void Test_Parse_Capture_ReturnsIdentifier(string value, string expectedExpression)
    {
        ValueExpression expression = ValueExpressionParser.Parse(CreateBuilder(), value);

        Assert.AreEqual(ValueExpressionKind.EagerCapture, expression.Kind);
        Assert.AreEqual(expectedExpression, expression.Expression);
    }

    [TestMethod]
    [DataRow("prefix &{name}")]
    [DataRow("&{name} suffix")]
    [DataRow("see *{name} here")]
    [DataRow("${name}&{other}")]
    [DataRow("&{1name}")]
    [DataRow("*{@name}")]
    [DataRow("*{@}")]
    [DataRow("*{@@}")]
    [DataRow("*{}")]
    public void Test_Parse_InvalidActiveReference_ThrowsFormatException(string value)
    {
        FormatException exception = Assert.ThrowsExactly<FormatException>(() => ValueExpressionParser.Parse(CreateBuilder(), value));

        Assert.IsFalse(string.IsNullOrWhiteSpace(exception.Message));
    }
}
