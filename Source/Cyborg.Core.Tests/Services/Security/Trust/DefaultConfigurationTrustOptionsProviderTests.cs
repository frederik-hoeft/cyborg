using Cyborg.Core.Configuration.Builders;
using Cyborg.Core.Services.Security.Trust;
using Cyborg.Core.Services.Security.Trust.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Tests.Services.Security.Trust;

[TestClass]
public sealed class DefaultConfigurationTrustOptionsProviderTests : CyborgCoreTestBase
{
    [TestMethod]
    public Task Test_Options_RecomposesConfiguredLeavesAsync() => TestWithDIAsync(
        assertion: services =>
        {
            IConfigurationTrustOptionsProvider provider = services.GetRequiredService<IConfigurationTrustOptionsProvider>();

            Assert.AreEqual(TrustEnforcementMode.LogOnly, provider.Options.EnforcementMode);
            Assert.IsEmpty(provider.Options.Policies);
        },
        buildConfiguration: configuration =>
        {
            configuration.AddDictionary(new Dictionary<string, object>
            {
                ["cyborg.services.trust"] = ConfigurationTrustOptions.Default,
            });
            configuration.AddDictionary(new Dictionary<string, object>
            {
                ["cyborg.services.trust.enforcement_mode"] = TrustEnforcementMode.LogOnly,
            });
        });
}
