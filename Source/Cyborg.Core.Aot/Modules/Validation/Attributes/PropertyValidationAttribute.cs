namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

internal abstract class PropertyValidationAttribute : Attribute
{
    /// <summary>
    /// Gets or sets whether the validation constraint applies to each immediate collection element instead of the collection property itself.
    /// </summary>
    public bool TargetsElements { get; set; }
}
