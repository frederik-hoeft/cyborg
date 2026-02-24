using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Shared.Model;

public sealed record ModuleWithEnvironment(ModuleReference Module, ModuleEnvironment? Environment);
