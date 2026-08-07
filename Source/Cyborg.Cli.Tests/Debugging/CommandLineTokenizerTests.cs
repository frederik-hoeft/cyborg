using Cyborg.Cli.Debugging;

namespace Cyborg.Cli.Tests.Debugging;

[TestClass]
public sealed class CommandLineTokenizerTests
{
    [TestMethod]
    public void TryTokenize_QuotedArgument_PreservesWhitespace()
    {
        bool result = CommandLineTokenizer.TryTokenize(
            "break at \"module group\"",
            out string[] arguments,
            out string? error);

        Assert.IsTrue(result);
        Assert.IsNull(error);
        CollectionAssert.AreEqual(
            new[] { "break", "at", "module group" },
            arguments);
    }

    [TestMethod]
    public void TryTokenize_RepeatedWhitespaceAndTabs_AreSeparators()
    {
        bool result = CommandLineTokenizer.TryTokenize(
            "  break\t  at   value  ",
            out string[] arguments,
            out string? error);

        Assert.IsTrue(result);
        Assert.IsNull(error);
        CollectionAssert.AreEqual(
            new[] { "break", "at", "value" },
            arguments);
    }

    [TestMethod]
    public void TryTokenize_EmptyQuotedArgument_IsPreserved()
    {
        bool result = CommandLineTokenizer.TryTokenize(
            "break at \"\"",
            out string[] arguments,
            out string? error);

        Assert.IsTrue(result);
        Assert.IsNull(error);
        CollectionAssert.AreEqual(
            new[] { "break", "at", string.Empty },
            arguments);
    }

    [TestMethod]
    public void TryTokenize_RegexBackslashes_ArePreserved()
    {
        bool result = CommandLineTokenizer.TryTokenize(
            @"break at ^cyborg\.modules\.empty\.v1$",
            out string[] arguments,
            out string? error);

        Assert.IsTrue(result);
        Assert.IsNull(error);
        Assert.AreEqual(@"^cyborg\.modules\.empty\.v1$", arguments[2]);
    }

    [TestMethod]
    public void TryTokenize_BackslashBeforeInactiveQuote_IsPreserved()
    {
        bool result = CommandLineTokenizer.TryTokenize(
            "break at \"regex\\'value\"",
            out string[] arguments,
            out string? error);

        Assert.IsTrue(result);
        Assert.IsNull(error);
        Assert.AreEqual("regex\\'value", arguments[2]);
    }

    [TestMethod]
    public void TryTokenize_EscapedQuote_IsUnescaped()
    {
        bool result = CommandLineTokenizer.TryTokenize(
            "break at \"module \\\"quoted\\\"\"",
            out string[] arguments,
            out string? error);

        Assert.IsTrue(result);
        Assert.IsNull(error);
        Assert.AreEqual("module \"quoted\"", arguments[2]);
    }

    [TestMethod]
    public void TryTokenize_UnterminatedQuote_ReturnsError()
    {
        bool result = CommandLineTokenizer.TryTokenize(
            "break at \"module",
            out string[] arguments,
            out string? error);

        Assert.IsFalse(result);
        Assert.AreEqual(0, arguments.Length);
        Assert.IsNotNull(error);
    }
}
