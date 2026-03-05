namespace Cyborg.Core.Modules.Configuration.Model;

public sealed record ModuleContext(ModuleReference Module, ModuleEnvironment? Environment, ModuleReference? Configuration);