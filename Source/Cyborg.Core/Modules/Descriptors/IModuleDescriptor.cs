using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Modules.Descriptors.Builders;

namespace Cyborg.Core.Modules.Descriptors;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(
    ModuleValidationGeneratorContract.IModuleDescriptor)]
public interface IModuleDescriptor
{
    ValueTask DescribeAsync(
        IObjectDescriptionBuilder descriptionBuilder,
        CancellationToken cancellationToken);
}
