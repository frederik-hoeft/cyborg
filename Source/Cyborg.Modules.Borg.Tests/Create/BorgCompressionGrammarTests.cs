using Cyborg.Modules.Borg.Create;
using MSAssert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Cyborg.Modules.Borg.Tests.Create;

[TestClass]
public sealed class BorgCompressionGrammarTests : BorgModuleTestBase
{
    [TestMethod]
    [DataRow("lz4", true)]
    [DataRow("zstd,[22]", true)]
    [DataRow("auto,zlib,[9]", true)]
    [DataRow("zstd,23", false)]
    [DataRow("lz4garbage", false)]
    [DataRow("auto,zlib,9tail", false)]
    public Task TestValidationAsync_CompressionGrammar_RequiresCompleteMatchAsync(string compression, bool expectedValid) =>
        TestValidationAsync<BorgCreateModule>(
            $$"""
            {
              "cyborg.modules.borg.create.v1.4": {
                "archive_name": "test-archive",
                "source_path": "/tmp",
                "compression": "{{compression}}"
              }
            }
            """,
            result =>
            {
                bool hasGrammarError = result.Errors.Any(error => error.Rule == "match_grammar" && error.PropertyName == nameof(BorgCreateModule.Compression));
                MSAssert.AreEqual(!expectedValid, hasGrammarError, string.Join("; ", result.Errors.Select(error => error.ToString())));
            });
}
