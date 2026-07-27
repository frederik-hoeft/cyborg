using Cyborg.Core.Modules.Debugging;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class ModuleIdentityTests
{
    [TestMethod]
    public void Format_IncludesIdNameAndGroup()
    {
        string result = ModuleIdentity.Format("cyborg.modules.sequence.v1", "main", "backup");
        Assert.AreEqual("cyborg.modules.sequence.v1 name=main group=backup", result);
    }

    [TestMethod]
    public void Format_OmitsMissingOptionalFields()
    {
        string result = ModuleIdentity.Format("cyborg.modules.empty.v1", name: null, group: null);
        Assert.AreEqual("cyborg.modules.empty.v1", result);
    }
}
