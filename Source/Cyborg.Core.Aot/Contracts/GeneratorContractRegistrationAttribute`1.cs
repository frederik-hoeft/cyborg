namespace Cyborg.Core.Aot.Contracts;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
internal sealed class GeneratorContractRegistrationAttribute<T>(T type) : Attribute where T : unmanaged, Enum
{
    public T Type { get; } = type;
}
