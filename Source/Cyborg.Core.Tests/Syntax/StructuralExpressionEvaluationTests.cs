using Cyborg.Core.Runtime.Engine.Expressions;
using Cyborg.Core.Runtime.Engine.Expressions.Bound;
using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;
using Cyborg.Core.Runtime.Engine.Expressions.Grammars;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;
using Cyborg.Core.Text;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Tests.Syntax;

[TestClass]
public sealed class StructuralExpressionEvaluationTests
{
    private readonly ExpressionMaterializer _materializer = new();

    [TestMethod]
    public void Test_LazyReferencesObserveCurrentContextAndDoNotMutateBoundValues()
    {
        TestResolutionContext scope = new();
        scope.Set("name", "Alice");
        BoundValue template = ValueGrammar.Instance.Parse("Hello ${name}").Bind(scope);
        BoundValue indirection = ValueGrammar.Instance.Parse("&{name}").Bind(scope);

        Assert.AreEqual("Hello Alice", Materialize(template, scope));
        Assert.AreEqual("Alice", Materialize(indirection, scope));

        scope.Set("name", "Bob");
        Assert.AreEqual("Hello Bob", Materialize(template, scope));
        Assert.AreEqual("Bob", Materialize(indirection, scope));
    }

    [TestMethod]
    public void Test_EagerCaptureRetainsOriginalTerminalValue()
    {
        TestResolutionContext scope = new();
        scope.Set("source", "${other}");
        BoundValue snapshot = ValueGrammar.Instance.Parse("*{source}").Bind(scope);

        scope.Set("source", "changed");
        scope.Set("other", "resolved");
        Assert.AreEqual("${other}", Materialize(snapshot, scope));
    }

    [TestMethod]
    public void Test_EagerCaptureAndTerminalObjectsPreserveObjectIdentity()
    {
        List<int> value = [1, 2, 3];
        TestResolutionContext scope = new();
        scope.Set("source", value);
        BoundValue capture = ValueGrammar.Instance.Parse("*{source}").Bind(scope);
        BoundValue terminal = ValueGrammar.Instance.Compile(value).Bind(scope);

        Assert.AreSame(value, Materialize(capture, scope));
        Assert.AreSame(value, Materialize(terminal, scope));
        Assert.IsNull(Materialize(ValueGrammar.Instance.Compile(null).Bind(scope), scope));
    }

    [TestMethod]
    public void Test_MissingTextReferenceRemainsLazyButTypedReferenceFails()
    {
        TestResolutionContext scope = new();
        BoundValue textual = ValueGrammar.Instance.Parse("${missing}").Bind(scope);
        BoundValue typed = ValueGrammar.Instance.Parse("&{missing}").Bind(scope);

        Assert.AreEqual("${missing}", Materialize(textual, scope));
        Assert.ThrowsExactly<InvalidOperationException>(() => Materialize(typed, scope));
        Assert.ThrowsExactly<InvalidOperationException>(() => ValueGrammar.Instance.Parse("*{missing}").Bind(scope));

        scope.Set("missing", "found");
        Assert.AreEqual("found", Materialize(textual, scope));
        Assert.AreEqual("found", Materialize(typed, scope));
    }

    [TestMethod]
    [DataRow("${#name}", "${name}")]
    [DataRow("${##name}", "${#name}")]
    [DataRow("&{###name}", "&{##name}")]
    [DataRow("*{#name}", "*{name}")]
    public void Test_EscapesConsumeExactlyOneLayerWithoutInterpretingExposedTokens(string text, string expected)
    {
        TestResolutionContext scope = new();
        scope.Set("name", "interpreted");
        BoundValue expression = ValueGrammar.Instance.Parse(text).Bind(scope);
        Assert.AreEqual(expected, Materialize(expression, scope));
        Assert.AreEqual(expected, Materialize(expression, scope));
    }

    [TestMethod]
    public void Test_SplicedTextIsTerminalAndNeverParsedAgain()
    {
        TestResolutionContext scope = new();
        scope.Set("source", "${other}");
        scope.Set("other", "changed");
        BoundValue expression = ValueGrammar.Instance.Parse("prefix ${source} suffix").Bind(scope);
        Assert.AreEqual("prefix ${other} suffix", Materialize(expression, scope));
    }

    [TestMethod]
    public void Test_TaggedStringKindAndSecretTagsSurviveInterpolationAndTypedCapture()
    {
        TestResolutionContext scope = new();
        scope.Set("secret", new TaggedString("sensitive", ["secret"]));
        scope.Set("name", "plain");
        BoundValue template = ValueGrammar.Instance.Parse("${secret}/${name}").Bind(scope);
        TaggedString materialized = Assert.IsInstanceOfType<TaggedString>(Materialize(template, scope));
        Assert.AreEqual("sensitive/plain", materialized.Value);
        Assert.IsTrue(materialized.HasTag("secret"));

        BoundValue taggedText = ValueGrammar.Instance.Parse(new TaggedString("Hello ${name}")).Bind(scope);
        TaggedString typed = Assert.IsInstanceOfType<TaggedString>(Materialize(taggedText, scope));
        Assert.AreEqual("Hello plain", typed.Value);
        Assert.IsFalse(typed.HasTags);

        BoundValue capture = ValueGrammar.Instance.Parse(new TaggedString("*{secret}", ["additional"])).Bind(scope);
        TaggedString captured = Assert.IsInstanceOfType<TaggedString>(Materialize(capture, scope));
        Assert.IsTrue(captured.HasTag("secret"));
        Assert.IsTrue(captured.HasTag("additional"));
    }

    [TestMethod]
    public void Test_ContextualAndEntryPointReferencesArePassedToResolutionContext()
    {
        TestResolutionContext scope = new();
        scope.Set("@", "module-local");
        scope.Set("@@", "entry-module");
        scope.Set("@config", "entry-config");
        Assert.AreEqual("module-local", Materialize(ValueGrammar.Instance.Parse("${@}").Bind(scope), scope));
        Assert.AreEqual("entry-module", Materialize(ValueGrammar.Instance.Parse("${@@}").Bind(scope), scope));
        Assert.AreEqual("entry-config", Materialize(ValueGrammar.Instance.Parse("&{@config}").Bind(scope), scope));
    }

    private object? Materialize(BoundValue bound, IExpressionResolutionContext scope) => _materializer.Materialize(bound.Evaluate(scope));

    private sealed class TestResolutionContext : IExpressionResolutionContext
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

        public void Set(string key, object? value) => _values[key] = value;

        public bool TryResolve(VariableReferenceSyntax reference, [NotNullWhen(true)] out EvaluatedValue? value)
        {
            string name = reference.ToString();
            if (_values.TryGetValue(name, out object? resolved) && resolved is not null)
            {
                value = new EvaluatedTerminal(resolved);
                return true;
            }
            value = null;
            return false;
        }
    }
}
