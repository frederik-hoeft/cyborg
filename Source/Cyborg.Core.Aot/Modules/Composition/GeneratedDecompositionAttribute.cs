namespace Cyborg.Core.Aot.Modules.Composition;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class GeneratedDecompositionAttribute : Attribute
{
    public Type? NamingPolicyProvider { get; set; }

    public string? NamingPolicy { get; set; }
}