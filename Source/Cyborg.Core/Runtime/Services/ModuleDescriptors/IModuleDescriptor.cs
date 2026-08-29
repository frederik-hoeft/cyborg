using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Runtime.Services.ModuleDescriptors.Builders;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IModuleDescriptor)]
public interface IModuleDescriptor
{
    ValueTask DescribeAsync(IObjectDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken);
}
