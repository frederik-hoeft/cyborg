using Cyborg.Modules.Borg.Prune.Metrics;
using Cyborg.Modules.Borg.Prune.Metrics.Model;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Parsing.Visitors;
using Cyborg.Core.Parsing.Parsers;
using MSAssert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Cyborg.Modules.Borg.Tests.Prune;

[TestClass]
public sealed class BorgPruneLineGrammarTests
{
    private const string ARCHIVE_ID = "c470fb03260ea70a4a81cb9cd25c9a1570b7ec784d464c878d926317005bc1a2";

    [TestMethod]
    public void Test_ParsePruningArchive()
    {
        string line = $"Pruning archive (1/1):                       teamspeak-2026-02-01T04:50:18        Sun, 2026-02-01 04:50:19 [{ARCHIVE_ID}]";

        MSAssert.IsTrue(BorgPruneLineGrammar.TryParse(line, out BorgPruneLineModel? model));
        MSAssert.IsNotNull(model);
        MSAssert.IsInstanceOfType<BorgPrunePruneAction>(model.Action);
        MSAssert.AreEqual(new BorgPrunePruneAction(1, 1), model.Action);
        MSAssert.AreEqual("teamspeak-2026-02-01T04:50:18", model.ArchiveName);
        MSAssert.AreEqual(ARCHIVE_ID, model.ArchiveId);
        MSAssert.AreEqual(new DateTime(2026, 2, 1, 4, 50, 19), model.ArchiveTimestamp);
    }

    [TestMethod]
    public void Test_ParseKeepingArchive()
    {
        string line = $"Keeping archive (rule: monthly #1):          teamspeak-2026-01-28T20:28:09        Wed, 2026-01-28 20:28:10 [{ARCHIVE_ID}]";

        MSAssert.IsTrue(BorgPruneLineGrammar.TryParse(line, out BorgPruneLineModel? model));
        MSAssert.IsNotNull(model);
        MSAssert.AreEqual(new BorgPruneKeepAction("monthly", 1), model.Action);
    }

    [TestMethod]
    public void Test_RegexTerminal_RespectsNonzeroOffset()
    {
        ArchiveName parser = new();

        MSAssert.IsTrue(parser.TryParse("prefix archive-name rest", 7, out ISyntaxNode? node, out int charsConsumed));
        MSAssert.AreEqual(12, charsConsumed);
        MSAssert.IsNotNull(node);
        MSAssert.IsFalse(parser.TryParse("prefix archive-name", 6, out _, out _));
    }

    [TestMethod]
    public void Test_TypedVisitorContract_RejectsIncompatibleVisitors()
    {
        ArchiveName parser = new();
        MSAssert.IsTrue(parser.CanAccept<BorgPruneVisitor>());
        MSAssert.IsFalse(parser.CanAccept<UnrelatedVisitor>());
        MSAssert.ThrowsExactly<InvalidOperationException>(() => parser.RequireVisitor<UnrelatedVisitor>());

        MSAssert.IsTrue(parser.TryParse("archive-name", 0, out ISyntaxNode? node, out _));
        MSAssert.IsNotNull(node);
        MSAssert.ThrowsExactly<ArgumentException>(() => node.Accept(new UnrelatedVisitor()));
    }

    [TestMethod]
    public void Test_RejectTrailingGarbage()
    {
        string line = $"Pruning archive (1/1): teamspeak-2026-02-01T04:50:18 Sun, 2026-02-01 04:50:19 [{ARCHIVE_ID}] garbage";

        MSAssert.IsFalse(BorgPruneLineGrammar.TryParse(line, out _));
    }

    private sealed class UnrelatedVisitor : INodeVisitor;
}
