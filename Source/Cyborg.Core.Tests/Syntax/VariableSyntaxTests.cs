using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using System.Text.Json;

namespace Cyborg.Core.Tests.Syntax;

[TestClass]
public sealed class VariableSyntaxTests
{
    private static VariableSyntaxBuilder CreateBuilder() => new(JsonNamingPolicy.SnakeCaseLower);

    [TestMethod]
    [DataRow("a.", false)]
    [DataRow("a..b", false)]
    [DataRow("a-", true)]
    [DataRow("a-.b", true)]
    [DataRow("a.-b", true)]
    [DataRow("a.-", true)]
    [DataRow("-", true)]
    [DataRow("--", true)]
    [DataRow("a--b", true)]
    [DataRow("-.0", true)]
    [DataRow("a", true)]
    [DataRow("Z", true)]
    [DataRow("_", true)]
    [DataRow("__", true)]
    [DataRow("a0", true)]
    [DataRow("_0", true)]
    [DataRow("snake_case", true)]
    [DataRow("kebab-case", true)]
    [DataRow("dotted.name", true)]
    [DataRow("mixed_Name-1.2", true)]
    [DataRow("", false)]
    [DataRow(" ", false)]
    [DataRow("\t", false)]
    [DataRow("\r\n", false)]
    [DataRow("1name", false)]
    [DataRow("-name", true)]
    [DataRow(".name", false)]
    [DataRow("@", false)]
    [DataRow("@name", false)]
    [DataRow("@@", false)]
    [DataRow("${name}", false)]
    [DataRow("name value", false)]
    [DataRow("name/value", false)]
    [DataRow("name:value", false)]
    [DataRow("name$value", false)]
    [DataRow("name{value}", false)]
    [DataRow("name}", false)]
    [DataRow("\u00E4", false)]
    [DataRow("name\u00E4", false)]
    [DataRow("name\0", false)]
    [DataRow("name\n", false)]
    [DataRow("name\r\n", false)]
    public void Test_IdentifierRegex_ReturnsExpectedMatch(string value, bool expected)
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        bool actual = builder.IdentifierRegex.IsMatch(value);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("${a.}", false, "")]
    [DataRow("${a..b}", false, "")]
    [DataRow("${a-}", true, "a-")]
    [DataRow("${a-.b}", true, "a-.b")]
    [DataRow("${a.-b}", true, "a.-b")]
    [DataRow("${a.-}", true, "a.-")]
    [DataRow("${-}", true, "-")]
    [DataRow("${--}", true, "--")]
    [DataRow("${-.0}", true, "-.0")]
    [DataRow("${name}", true, "name")]
    [DataRow("hey ${name}", false, "")]
    [DataRow("${name} wassup", false, "")]
    [DataRow(" ${name}", false, "")]
    [DataRow("${_}", true, "_")]
    [DataRow("${a0}", true, "a0")]
    [DataRow("${a-b.c_0}", true, "a-b.c_0")]
    [DataRow("${name.}", false, "")]
    [DataRow("${name-}", true, "name-")]
    [DataRow("${@}", true, "@")]
    [DataRow("${@@}", true, "@@")]
    [DataRow("${@name}", true, "@name")]
    [DataRow("${@_}", true, "@_")]
    [DataRow("${@a-b.c_0}", true, "@a-b.c_0")]
    [DataRow("${@-}", true, "@-")]
    [DataRow("${@--}", true, "@--")]
    [DataRow("${@-.0}", true, "@-.0")]
    [DataRow("", false, "")]
    [DataRow("name", false, "")]
    [DataRow("$name", false, "")]
    [DataRow("${}", false, "")]
    [DataRow("${1name}", false, "")]
    [DataRow("${@1name}", false, "")]
    [DataRow("${@@name}", false, "")]
    [DataRow("${@@.name}", false, "")]
    [DataRow("${-name}", true, "-name")]
    [DataRow("${.name}", false, "")]
    [DataRow("${name value}", false, "")]
    [DataRow("${name/value}", false, "")]
    [DataRow("${#name}", false, "")]
    [DataRow("${##name}", false, "")]
    [DataRow("$${name}", false, "")]
    [DataRow("prefix ${name}", false, "")]
    [DataRow("${name} suffix", false, "")]
    [DataRow("${name}${other}", false, "")]
    [DataRow("${name", false, "")]
    [DataRow("$ {name}", false, "")]
    [DataRow("${name}}", false, "")]
    [DataRow("${\u00E4}", false, "")]
    [DataRow("${name}\n", false, "")]
    [DataRow("${name}\r\n", false, "")]
    public void Test_IndirectionRegex_ReturnsExpectedMatch(string value, bool expected, string expectedExpression)
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        System.Text.RegularExpressions.Match match =
            builder.IndirectionRegex.Match(value);

        Assert.AreEqual(expected, match.Success);
        if (!expected)
        {
            return;
        }

        Assert.AreEqual(0, match.Index);
        Assert.AreEqual(value.Length, match.Length);
        Assert.AreEqual(expectedExpression, match.Groups["expression"].Value);
    }

    [TestMethod]
    [DataRow("${a.}", 0, "")]
    [DataRow("${a..b}", 0, "")]
    [DataRow("${a-}", 1, "a-")]
    [DataRow("${a-.b}", 1, "a-.b")]
    [DataRow("${a.-b}", 1, "a.-b")]
    [DataRow("${a.-}", 1, "a.-")]
    [DataRow("${-}", 1, "-")]
    [DataRow("${--}", 1, "--")]
    [DataRow("${-.0}", 1, "-.0")]
    [DataRow("before ${a..b} after ${valid}", 1, "valid")]
    [DataRow("", 0, "")]
    [DataRow("plain text", 0, "")]
    [DataRow("${name}", 1, "name")]
    [DataRow("prefix ${name} suffix", 1, "name")]
    [DataRow("${_}", 1, "_")]
    [DataRow("${a-b.c_0}", 1, "a-b.c_0")]
    [DataRow("${name.}", 0, "")]
    [DataRow("${name-}", 1, "name-")]
    [DataRow("${@}", 1, "@")]
    [DataRow("${@@}", 1, "@@")]
    [DataRow("${@name}", 1, "@name")]
    [DataRow("${@-}", 1, "@-")]
    [DataRow("${@--}", 1, "@--")]
    [DataRow("${first}-${second}", 2, "first|second")]
    [DataRow("${first}${@second}${@@}", 3, "first|@second|@@")]
    [DataRow("${valid} ${invalid value} ${also_valid}", 2, "valid|also_valid")]
    [DataRow("${name}${}", 1, "name")]
    [DataRow("${}${name}", 1, "name")]
    [DataRow("${}", 0, "")]
    [DataRow("${1name}", 0, "")]
    [DataRow("${@1name}", 0, "")]
    [DataRow("${@@name}", 0, "")]
    [DataRow("${-name}", 1, "-name")]
    [DataRow("${.name}", 0, "")]
    [DataRow("${name value}", 0, "")]
    [DataRow("${name/value}", 0, "")]
    [DataRow("${#name}", 0, "")]
    [DataRow("${##name}", 0, "")]
    [DataRow("before ${#name} after ${name}", 1, "name")]
    [DataRow("$${name}", 1, "name")]
    [DataRow("${name}\n", 1, "name")]
    [DataRow("\n${name}\n", 1, "name")]
    [DataRow("${name\n}", 0, "")]
    [DataRow("${name", 0, "")]
    [DataRow("$ {name}", 0, "")]
    [DataRow("${name}}", 1, "name")]
    [DataRow("{{${name}}}", 1, "name")]
    [DataRow("${\u00E4}", 0, "")]
    public void Test_InterpolationRegex_Input_ReturnsExpectedMatches(string value, int expectedCount, string expectedExpressions)
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        System.Text.RegularExpressions.MatchCollection matches = builder.InterpolationRegex.Matches(value);
        List<string> actualExpressions = new(matches.Count);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            actualExpressions.Add(match.Groups["expression"].Value);
        }

        Assert.HasCount(expectedCount, matches);
        Assert.AreEqual(expectedExpressions, string.Join("|", actualExpressions));
    }

    [TestMethod]
    [DataRow("${#HOME}", 1, "#|HOME")]
    [DataRow("${##HOME}", 1, "##|HOME")]
    [DataRow("${#}", 1, "#|")]
    [DataRow("before ${#HOME} after ${##USER}", 2, "#|HOME;##|USER")]
    [DataRow("${HOME}", 0, "")]
    [DataRow("${#HOME", 0, "")]
    public void Test_HashLiteralRegex_Input_ReturnsExpectedMatches(string value, int expectedCount, string expectedMatches)
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        System.Text.RegularExpressions.MatchCollection matches = builder.HashLiteralRegex.Matches(value);
        List<string> actualMatches = new(matches.Count);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            actualMatches.Add($"{match.Groups["hashes"].Value}|{match.Groups["content"].Value}");
        }

        Assert.HasCount(expectedCount, matches);
        Assert.AreEqual(expectedMatches, string.Join(";", actualMatches));
    }

    [TestMethod]
    [DataRow("root.-child", true)]
    [DataRow("root.child-", true)]
    [DataRow("root.--", true)]
    [DataRow("root.child-.leaf", true)]
    [DataRow("root.-child.leaf", true)]
    [DataRow("-", true)]
    [DataRow("--", true)]
    [DataRow("root.-", true)]
    [DataRow("root.0-", true)]
    [DataRow("a", true)]
    [DataRow("Z", true)]
    [DataRow("0", false)]
    [DataRow("123", false)]
    [DataRow("_1", true)]
    [DataRow("-1", true)]
    [DataRow("a1", true)]
    [DataRow("_", true)]
    [DataRow("__", true)]
    [DataRow("abc_123", true)]
    [DataRow("a.b", true)]
    [DataRow("1.segment", false)]
    [DataRow("_._", true)]
    [DataRow("root.child", true)]
    [DataRow("root.child-1", true)]
    [DataRow("root.0", true)]
    [DataRow("root._", true)]
    [DataRow("root.child_name", true)]
    [DataRow("a.b-c.d_e-1", true)]
    [DataRow("", false)]
    [DataRow(" ", false)]
    [DataRow("\t", false)]
    [DataRow(".root", false)]
    [DataRow("root.", false)]
    [DataRow("root..child", false)]
    [DataRow("-root", true)]
    [DataRow("root-name", true)]
    [DataRow("root-name.child", true)]
    [DataRow("1-root.child", false)]
    [DataRow("root/child", false)]
    [DataRow("root:child", false)]
    [DataRow("root child", false)]
    [DataRow("root@child", false)]
    [DataRow("${root}", false)]
    [DataRow("\u00E4", false)]
    [DataRow("root.\u00E4", false)]
    [DataRow("root\0", false)]
    [DataRow("root\n", false)]
    [DataRow("root\r\n", false)]
    public void Test_NamespaceRegex_ReturnsExpectedMatch(string value, bool expected)
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        bool actual = builder.NamespaceRegex.IsMatch(value);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("identifier", true)]
    [DataRow("identifier.", false)]
    [DataRow("identifier..child", false)]
    [DataRow("identifier-", true)]
    [DataRow("identifier-.child", true)]
    [DataRow("identifier.-child", true)]
    [DataRow("-", true)]
    [DataRow("--", true)]
    [DataRow("identifier--child", true)]
    [DataRow("identifier.-", true)]
    [DataRow("_", true)]
    [DataRow("_private", true)]
    [DataRow("identifier_123", true)]
    [DataRow("identifier-with-hyphen", true)]
    [DataRow("identifier.with.path", true)]
    [DataRow("", false)]
    [DataRow(" ", false)]
    [DataRow("   ", false)]
    [DataRow("\t", false)]
    [DataRow("\t\r\n", false)]
    [DataRow(" identifier", false)]
    [DataRow("identifier ", false)]
    [DataRow("1identifier", false)]
    [DataRow("-identifier", true)]
    [DataRow(".identifier", false)]
    [DataRow("@identifier", false)]
    [DataRow("${identifier}", false)]
    [DataRow("identifier/path", false)]
    [DataRow("identifier value", false)]
    [DataRow("\u00E4", false)]
    [DataRow("identifier\n", false)]
    public void Test_IsValidIdentifier_ReturnsExpectedResult(string value, bool expected)
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        bool actual = builder.IsValidIdentifier(value);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("namespace", true)]
    [DataRow("namespace.-child", true)]
    [DataRow("namespace.child-", true)]
    [DataRow("namespace.--", true)]
    [DataRow("namespace.child-.leaf", true)]
    [DataRow("namespace.-child.leaf", true)]
    [DataRow("-", true)]
    [DataRow("--", true)]
    [DataRow("namespace.-", true)]
    [DataRow("namespace.0-", true)]
    [DataRow("0", false)]
    [DataRow("123", false)]
    [DataRow("_1", true)]
    [DataRow("-1", true)]
    [DataRow("a1", true)]
    [DataRow("_", true)]
    [DataRow("namespace_1", true)]
    [DataRow("namespace.child", true)]
    [DataRow("1.child", false)]
    [DataRow("namespace.child-name", true)]
    [DataRow("namespace.child-name.grand_child-2", true)]
    [DataRow("", false)]
    [DataRow(" ", false)]
    [DataRow("   ", false)]
    [DataRow("\t\r\n", false)]
    [DataRow(" namespace", false)]
    [DataRow("namespace ", false)]
    [DataRow(".namespace", false)]
    [DataRow("namespace.", false)]
    [DataRow("namespace..child", false)]
    [DataRow("-namespace", true)]
    [DataRow("namespace-name", true)]
    [DataRow("namespace name", false)]
    [DataRow("namespace/child", false)]
    [DataRow("namespace:child", false)]
    [DataRow("@namespace", false)]
    [DataRow("${namespace}", false)]
    [DataRow("\u00E4", false)]
    [DataRow("namespace.\u00E4", false)]
    [DataRow("namespace\n", false)]
    public void Test_IsValidNamespace_ReturnsExpectedResult(string value, bool expected)
    {
        VariableSyntaxBuilder builder = CreateBuilder();

        bool actual = builder.IsValidNamespace(value);

        Assert.AreEqual(expected, actual);
    }
}
