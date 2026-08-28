using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Core.Runtime.Services.Debugging.Configuration;

public sealed class DebugOptionsProvider() : DynamicValueProviderBase<DebugOptions>("cyborg.types.core.debug.options.v1");
