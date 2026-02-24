namespace Cyborg.Core.Modules.Runtime;

public enum EnvironmentScope
{
    Isolated,
    Global,
    InheritParent,
    InheritGlobal,
    Parent,
    Reference
}