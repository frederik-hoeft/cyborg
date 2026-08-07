using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Modules.Descriptors.Builders;

public sealed class DIObjectDescriptionBuilderFactory(IServiceProvider serviceProvider) : IObjectDescriptionBuilderFactory
{
    public IObjectDescriptionBuilder CreateBuilder() => serviceProvider.GetRequiredService<IObjectDescriptionBuilder>();
}
