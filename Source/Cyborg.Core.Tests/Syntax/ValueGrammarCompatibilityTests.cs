using Cyborg.Core.Runtime.Engine.Expressions.Grammars;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;

namespace Cyborg.Core.Tests.Syntax;

[TestClass]
public sealed class ValueGrammarCompatibilityTests
{
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
        ValueSyntax expression = ValueGrammar.Instance.Parse(value);

        Assert.IsInstanceOfType<TextValueSyntax>(expression);
    }

    [TestMethod]
    [DataRow("&{name}", "name")]
    [DataRow("&{@name}", "@name")]
    [DataRow("&{@}", "@")]
    [DataRow("&{@@}", "@@")]
    [DataRow("&{host.port}", "host.port")]
    public void Test_Parse_Indirection_ReturnsExpression(string value, string expectedExpression)
    {
        ValueSyntax expression = ValueGrammar.Instance.Parse(value);

        TypedReferenceSyntax reference = Assert.IsInstanceOfType<TypedReferenceSyntax>(expression);
        Assert.AreEqual(expectedExpression, reference.Reference.ToString());
    }

    [TestMethod]
    [DataRow("*{name}", "name")]
    [DataRow("*{host.port}", "host.port")]
    public void Test_Parse_Capture_ReturnsIdentifier(string value, string expectedExpression)
    {
        ValueSyntax expression = ValueGrammar.Instance.Parse(value);

        CaptureSyntax reference = Assert.IsInstanceOfType<CaptureSyntax>(expression);
        Assert.AreEqual(expectedExpression, reference.Reference.ToString());
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
        FormatException exception = Assert.ThrowsExactly<FormatException>(() => ValueGrammar.Instance.Parse(value));

        Assert.IsFalse(string.IsNullOrWhiteSpace(exception.Message));
    }
}
