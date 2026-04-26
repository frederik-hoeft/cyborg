using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Modules.Subprocess;

public sealed class EnvironmentVariableValueProvider() : DynamicValueProviderBase<EnvironmentVariable>("cyborg.types.env.var.v1");
