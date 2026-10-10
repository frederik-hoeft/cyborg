using Cyborg.Core.Parsing;
using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Parsing.Visitors;
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
    [DataRow("ABC")]
    [DataRow("ACB")]
    [DataRow("BAC")]
    [DataRow("BCA")]
    [DataRow("CAB")]
    [DataRow("CBA")]
    public void Test_Set_AcceptsPermutationsAndPreservesInputOrder(string input)
    {
        IParser grammar = Grammar.Set(new Literal("A"), new Literal("B"), new Literal("C")).NamedCopy("letters");

        Assert.IsTrue(grammar.TryParseComplete(input, out ISyntaxNode? node));
        Assert.IsInstanceOfType<SetSyntaxNode>(node);
        Assert.AreEqual("letters", node.Name);

        LiteralVisitor visitor = new();
        node.Accept(visitor);
        Assert.AreSequenceEqual(input.Select(character => character.ToString()), visitor.Values);
        Assert.IsTrue(visitor.Nodes.All(child => child.HasParent("letters")));
    }

    [TestMethod]
    public void Test_Set_AcceptsNonemptySubsetsAndRejectsUnmatchedSuffixes()
    {
        IParser grammar = Grammar.Set(new Literal("A"), new Literal("B"));

        Assert.IsTrue(grammar.TryParseComplete("B", out ISyntaxNode? node));
        Assert.IsInstanceOfType<SetSyntaxNode>(node);
        Assert.IsTrue(grammar.TryParse("prefixBAA", 6, out node, out int consumed));
        Assert.AreEqual(2, consumed);
        Assert.IsFalse(grammar.TryParseComplete("BAA", out _));
        Assert.IsFalse(grammar.TryParseComplete("ABA", out _));
    }

    [TestMethod]
    public void Test_Set_DoesNotMatchWhenNoMembersMatch()
    {
        IParser grammar = Grammar.Set(new Literal("A"), new Literal("B"));

        Assert.IsFalse(grammar.TryParse("C", 0, out ISyntaxNode? node, out int consumed));
        Assert.IsNull(node);
        Assert.AreEqual(0, consumed);
        Assert.IsFalse(grammar.TryParseComplete(string.Empty, out _));
    }

    [TestMethod]
    [DataRow("AB", true)]
    [DataRow("BA", true)]
    [DataRow("B", true)]
    [DataRow("", false)]
    public void Test_Set_IgnoresZeroWidthOptionalUntilItCanConsume(string input, bool expected)
    {
        IParser grammar = Grammar.Set(Grammar.Optional(new Literal("A")), new Literal("B"));

        bool success = grammar.TryParseComplete(input, out ISyntaxNode? node);
        Assert.AreEqual(expected, success);
        if (expected)
        {
            Assert.IsInstanceOfType<SetSyntaxNode>(node);
            LiteralVisitor visitor = new();
            node.Accept(visitor);
            Assert.AreEqual(input, string.Concat(visitor.Values));
        }
        else
        {
            Assert.IsNull(node);
        }
    }

    [TestMethod]
    [DataRow("AB", true)]
    [DataRow("BA", true)]
    [DataRow("B", true)]
    [DataRow("AAAB", true)]
    [DataRow("BAAA", true)]
    [DataRow("", false)]
    public void Test_Set_IgnoresZeroWidthRepeatUntilItCanConsume(string input, bool expected)
    {
        IParser grammar = Grammar.Set(Grammar.Repeat(new Literal("A")), new Literal("B"));

        bool success = grammar.TryParseComplete(input, out ISyntaxNode? node);
        Assert.AreEqual(expected, success);
        if (expected)
        {
            Assert.IsInstanceOfType<SetSyntaxNode>(node);
            LiteralVisitor visitor = new();
            node.Accept(visitor);
            Assert.AreEqual(input, string.Concat(visitor.Values));
        }
        else
        {
            Assert.IsNull(node);
        }
    }

    [TestMethod]
    public void Test_Set_ZeroWidthOnlyDoesNotCountAsSuccess()
    {
        IParser grammar = Grammar.Set(Grammar.Optional(new Literal("A")));

        Assert.IsFalse(grammar.TryParse(string.Empty, 0, out ISyntaxNode? node, out int consumed));
        Assert.IsNull(node);
        Assert.AreEqual(0, consumed);
        Assert.IsTrue(grammar.TryParseComplete("A", out _));
    }

    [TestMethod]
    public void Test_Set_DoesNotBacktrackOverConsumingOptionalMatch()
    {
        IParser grammar = Grammar.Set(Grammar.Optional(new Literal("A")), new Literal("AB"));

        Assert.IsTrue(grammar.TryParse("AB", 0, out _, out int consumed));
        Assert.AreEqual(1, consumed);
        Assert.IsFalse(grammar.TryParseComplete("AB", out _));
    }

    [TestMethod]
    public void Test_Set_TreatsDuplicateParserInstancesAsDistinctMembers()
    {
        Literal literal = new("A");
        IParser grammar = Grammar.Set(literal, literal);

        Assert.IsTrue(grammar.TryParseComplete("AA", out ISyntaxNode? node));
        Assert.IsInstanceOfType<SetSyntaxNode>(node);
        Assert.IsFalse(grammar.TryParseComplete("AAA", out _));
    }

    [TestMethod]
    public void Test_Set_FluentFactorySupportsUnorderedMembers()
    {
        IParser grammar = Grammar.Set(set => set.Parser(new Literal("A")).Parser(new Literal("B")));

        Assert.IsTrue(grammar.TryParseComplete("BA", out ISyntaxNode? node));
        Assert.IsInstanceOfType<SetSyntaxNode>(node);
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

    private sealed class LiteralNode(string? name, string value) : ResultSyntaxNodeBase<string>(name, value)
    {
        public override void Accept(INodeVisitor visitor)
        {
            base.Accept(visitor);
            if (visitor is LiteralVisitor literalVisitor)
            {
                literalVisitor.Values.Add(Evaluate());
                literalVisitor.Nodes.Add(this);
            }
        }
    }

    private sealed class LiteralVisitor : INodeVisitor
    {
        public List<string> Values { get; } = [];

        public List<ISyntaxNode> Nodes { get; } = [];
    }
}
