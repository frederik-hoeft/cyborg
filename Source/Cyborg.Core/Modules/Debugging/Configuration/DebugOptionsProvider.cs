using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Core.Modules.Debugging.Configuration;

public sealed class DebugOptionsProvider() : DynamicValueProviderBase<DebugOptions>("cyborg.types.core.debug.options.v1");
