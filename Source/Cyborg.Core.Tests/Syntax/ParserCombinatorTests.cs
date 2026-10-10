using Cyborg.Core.Parsing;
using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Tests.Syntax;

[TestClass]
public sealed class ParserCombinatorTests
{
    [TestMethod]
    public void Test_Sequence_RespectsOffsetsAndReportsRelativeConsumedLength()
    {
        IParser parser = Grammar.Sequence(new Literal("first"), new Literal("second"));

        Assert.IsTrue(parser.TryParse("prefixfirstsecondtail", 6, out ISyntaxNode? node, out int consumed));
        Assert.AreEqual(11, consumed);
        Assert.IsNotNull(node);
        Assert.IsFalse(parser.TryParse("prefixfirstother", 6, out _, out _));
    }

    [TestMethod]
    public void Test_CompleteParse_RejectsOtherwiseValidPrefixes()
    {
        IParser parser = new Literal("value");

        Assert.IsTrue(parser.TryParseComplete("value", out ISyntaxNode? node));
        Assert.IsNotNull(node);
        Assert.IsFalse(parser.TryParseComplete("value-extra", out ISyntaxNode? rejected));
        Assert.IsNull(rejected);
    }

    [TestMethod]
    public void Test_Repeat_ConsumesOneOrMoreMatches()
    {
        IParser grammar = Grammar.Repeat(new Literal("a"));

        Assert.IsTrue(grammar.TryParseComplete("aaa", out _));
        Assert.IsTrue(grammar.TryParseComplete(string.Empty, out _));
        Assert.IsFalse(grammar.TryParseComplete("aaab", out _));
    }

    [TestMethod]
    public void Test_Repeat_RejectsZeroWidthSuccess()
    {
        IParser grammar = Grammar.Repeat(Grammar.Optional(new Literal("a")));

        Assert.ThrowsExactly<InvalidOperationException>(() => grammar.TryParseComplete("aaab", out _));
    }

    [TestMethod]
    public void Test_Alternative_UsesFirstSuccessfulBranch()
    {
        IParser grammar = Grammar.Alternative(new Literal("a"), new Literal("ab"));

        Assert.IsTrue(grammar.TryParse("ab", 0, out _, out int consumed));
        Assert.AreEqual(1, consumed);
        Assert.IsFalse(grammar.TryParseComplete("ab", out _));
    }

    [TestMethod]
    public void Test_ResultNodes_RetainTypedValues()
    {
        IParser grammar = new Literal("abc");

        Assert.IsTrue(grammar.TryParseComplete("abc", out ISyntaxNode? node));
        Assert.IsInstanceOfType<LiteralNode>(node);
        Assert.AreEqual("abc", ((LiteralNode)node).Evaluate());
    }

    private sealed class Literal(string literal, string? name = null) : ParserBase
    {
        public override string? Name => name;

        public override IParser NamedCopy(string newName) => new Literal(literal, newName);

        public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)offset, (uint)input.Length);
            if (input.AsSpan(offset).StartsWith(literal, StringComparison.Ordinal))
            {
                charsConsumed = literal.Length;
                syntaxNode = new LiteralNode(Name, literal);
                return true;
            }
            charsConsumed = 0;
            syntaxNode = null;
            return false;
        }
    }

    private sealed class LiteralNode(string? name, string value) : ResultSyntaxNodeBase<string>(name, value);
}
