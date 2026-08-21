using Cyborg.Cli.Arguments;
using Cyborg.Core.Text;
using Cyborg.Core.Text.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Cli.Tests.Configuration;

[TestClass]
public sealed class DynamicArgumentLogRendererTests : CyborgCliTestBase
{
    [TestMethod]
    public Task Test_RenderDefinition_SecretValue_IsRedactedAsync() => TestWithDIAsync(services =>
    {
        DynamicArgumentLogRenderer renderer = services.GetRequiredService<DynamicArgumentLogRenderer>();

        string result = renderer.RenderDefinition($"token:{WellKnownDynamicValueTypes.Secret}=\"s3cret\"");

        Assert.Contains($"token:{WellKnownDynamicValueTypes.Secret}=", result);
        Assert.Contains(SecretTagHandler.RedactedDisplay, result);
        Assert.DoesNotContain("s3cret", result);
    });

    [TestMethod]
    public Task Test_RenderDefinition_UntypedValue_RemainsVisibleAsync() => TestWithDIAsync(services =>
    {
        DynamicArgumentLogRenderer renderer = services.GetRequiredService<DynamicArgumentLogRenderer>();

        string result = renderer.RenderDefinition("target=backup-host");

        Assert.AreEqual("target=backup-host", result);
    });

    [TestMethod]
    public Task Test_RenderDefinition_StructuredValue_OmitsRawPayloadAsync() => TestWithDIAsync(services =>
    {
        DynamicArgumentLogRenderer renderer = services.GetRequiredService<DynamicArgumentLogRenderer>();
        string definition = "cyborg.services.metrics:cyborg.types.services.metrics.v1={\"namespace\":\"test\",\"file_path\":\"/tmp/cyborg.prom\"}";

        string result = renderer.RenderDefinition(definition);

        Assert.AreEqual("cyborg.services.metrics:cyborg.types.services.metrics.v1=<structured>", result);
        Assert.DoesNotContain("/tmp/cyborg.prom", result);
    });
}
