using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Runtime.Engine.Expressions.Grammars;
using Cyborg.Core.Runtime.Engine.Expressions.Parsers;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;

namespace Cyborg.Core.Tests.Syntax;

[TestClass]
public sealed class StructuralExpressionGrammarTests
{
    [TestMethod]
    [DataRow("&{name}", "name", "CurrentScope")]
    [DataRow("&{host.port}", "host.port", "CurrentScope")]
    [DataRow("&{@name}", "name", "EntryPoint")]
    [DataRow("&{@}", null, "CurrentScope")]
    [DataRow("&{@@}", null, "EntryPoint")]
    public void Test_LazyTypedReference_HasStructuralTarget(string source, string? identifier, string anchor)
    {
        TypedReferenceSyntax expression = (TypedReferenceSyntax)ValueGrammar.Instance.Parse(source);

        Assert.AreEqual(identifier, expression.Reference.Identifier);
        Assert.AreEqual(anchor, expression.Reference.Anchor.ToString());
    }

    [TestMethod]
    [DataRow("*{name}", "name")]
    [DataRow("*{host.port}", "host.port")]
    public void Test_EagerCapture_HasStructuralTarget(string source, string identifier)
    {
        CaptureSyntax expression = (CaptureSyntax)ValueGrammar.Instance.Parse(source);

        Assert.AreEqual(identifier, expression.Reference.Identifier);
        Assert.AreEqual(ReferenceAnchor.CurrentScope, expression.Reference.Anchor);
    }

    [TestMethod]
    public void Test_Text_HasDistinctImmutableSegments()
    {
        TextValueSyntax expression = (TextValueSyntax)ValueGrammar.Instance.Parse("before ${name}/&{##literal} after ${@@}");

        Assert.HasCount(6, expression.Parts);
        Assert.AreEqual("before ", ((LiteralPartSyntax)expression.Parts[0]).Text);
        Assert.AreEqual("name", ((InterpolationPartSyntax)expression.Parts[1]).Reference.Identifier);
        Assert.AreEqual("/", ((LiteralPartSyntax)expression.Parts[2]).Text);
        EscapedPartSyntax escaped = (EscapedPartSyntax)expression.Parts[3];
        Assert.AreEqual('&', escaped.Operator);
        Assert.AreEqual(2, escaped.EscapeDepth);
        Assert.AreEqual("literal", escaped.Body);
        Assert.AreEqual(" after ", ((LiteralPartSyntax)expression.Parts[4]).Text);
        InterpolationPartSyntax last = (InterpolationPartSyntax)expression.Parts[5];
        Assert.IsNull(last.Reference.Identifier);
        Assert.AreEqual(ReferenceAnchor.EntryPoint, last.Reference.Anchor);
    }

    [TestMethod]
    [DataRow("${name}", "name", "CurrentScope")]
    [DataRow("${@name}", "name", "EntryPoint")]
    [DataRow("${@}", null, "CurrentScope")]
    [DataRow("${@@}", null, "EntryPoint")]
    public void Test_Interpolation_ReferenceScope(string source, string? identifier, string anchor)
    {
        TextValueSyntax expression = (TextValueSyntax)ValueGrammar.Instance.Parse(source);
        InterpolationPartSyntax reference = (InterpolationPartSyntax)expression.Parts[0];

        Assert.AreEqual(identifier, reference.Reference.Identifier);
        Assert.AreEqual(anchor, reference.Reference.Anchor.ToString());
    }

    [TestMethod]
    [DataRow("${#name}", '$', 1, "name")]
    [DataRow("${##name}", '$', 2, "name")]
    [DataRow("&{#name}", '&', 1, "name")]
    [DataRow("*{#@name}", '*', 1, "@name")]
    public void Test_Escapes_RemainStructural(string source, char operatorSymbol, int depth, string body)
    {
        TextValueSyntax expression = (TextValueSyntax)ValueGrammar.Instance.Parse(source);
        EscapedPartSyntax part = (EscapedPartSyntax)expression.Parts[0];

        Assert.AreEqual(operatorSymbol, part.Operator);
        Assert.AreEqual(depth, part.EscapeDepth);
        Assert.AreEqual(body, part.Body);
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
    public void Test_InvalidActiveTypedFragments_AreRejected(string source) =>
        Assert.ThrowsExactly<FormatException>(() => ValueGrammar.Instance.Parse(source));

    [TestMethod]
    [DataRow("")]
    [DataRow("plain")]
    [DataRow("${not valid}")]
    [DataRow("literal &{#name} and *{#other}")]
    public void Test_PlainAndLiteralExpressions_AreText(string source) =>
        Assert.IsInstanceOfType<TextValueSyntax>(ValueGrammar.Instance.Parse(source));

    [TestMethod]
    public void Test_TextOnlyGrammar_RejectsTypedValues()
    {
        Assert.ThrowsExactly<FormatException>(() => ValueGrammar.Instance.ParseText("&{name}"));
        Assert.ThrowsExactly<FormatException>(() => ValueGrammar.Instance.ParseText("*{name}"));
        Assert.IsInstanceOfType<TextValueSyntax>(ValueGrammar.Instance.ParseText("${name}"));
    }

    [TestMethod]
    [DataRow("*{1name}")]
    [DataRow("&{name.}")]
    [DataRow("&{.name}")]
    [DataRow("prefix *{name} suffix")]
    [DataRow("&{@bad key}")]
    public void Test_MalformedTypedSyntax_IsRejected(string source) =>
        Assert.ThrowsExactly<FormatException>(() => ValueGrammar.Instance.Parse(source));

    [TestMethod]
    [DataRow("${}")]
    [DataRow("${missing brace")]
    [DataRow("&{missing brace")]
    [DataRow("@not-an-expression")]
    public void Test_InvalidOrIncompleteTokensRemainLiteralWhereLegacySyntaxDoes(string source)
    {
        TextValueSyntax expression = (TextValueSyntax)ValueGrammar.Instance.Parse(source);

        Assert.HasCount(1, expression.Parts);
        Assert.AreEqual(source, ((LiteralPartSyntax)expression.Parts[0]).Text);
    }

    [TestMethod]
    public void Test_SpecializedParsers_HonorOffsetsAndConsumedLengths()
    {
        IParser parser = new InterpolationParser();
        Assert.IsTrue(parser.TryParse("prefix ${host.port} suffix", 7, out ISyntaxNode? node, out int consumed));
        Assert.AreEqual(12, consumed);
        Assert.IsNotNull(node);
        Assert.IsFalse(parser.TryParse("prefix ${host.port} suffix", 0, out _, out int rejected));
        Assert.AreEqual(0, rejected);
    }

    [TestMethod]
    public void Test_OrdinaryClrObjects_CanBeRepresentedWithoutParsing()
    {
        List<int> original = [1, 2, 3];
        TerminalValueSyntax expression = new(original);

        Assert.AreSame(original, expression.Value);
    }
}
