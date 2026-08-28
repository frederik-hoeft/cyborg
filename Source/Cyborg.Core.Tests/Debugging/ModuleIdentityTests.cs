using Cyborg.Core.Runtime.Services.Debugging;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class ModuleIdentityTests
{
    [TestMethod]
    public void Test_Format_IncludesIdNameAndGroup()
    {
        string result = ModuleIdentity.Format("cyborg.modules.sequence.v1", "main", "backup");
        Assert.AreEqual("cyborg.modules.sequence.v1 name=main group=backup", result);
    }

    [TestMethod]
    public void Test_Format_OmitsMissingOptionalFields()
    {
        string result = ModuleIdentity.Format("cyborg.modules.empty.v1", name: null, group: null);
        Assert.AreEqual("cyborg.modules.empty.v1", result);
    }
}
