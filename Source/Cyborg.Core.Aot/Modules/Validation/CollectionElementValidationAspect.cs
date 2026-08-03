namespace Cyborg.Core.Aot.Modules.Validation;

internal sealed class CollectionElementValidationAspect(PropertyValidationAspect validationAspect) : PropertyAspect
{
    public PropertyValidationAspect ValidationAspect { get; } = validationAspect ?? throw new ArgumentNullException(nameof(validationAspect));
}
