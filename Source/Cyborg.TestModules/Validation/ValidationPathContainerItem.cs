using Cyborg.Core.Aot.Modules.Validation.Attributes;

namespace Cyborg.TestModules.Validation;

[Validatable]
public sealed record ValidationPathContainerItem(ValidationPathTestItem Child);
