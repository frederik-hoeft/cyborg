using Cyborg.Core.Modules.Configuration.Serialization.Dynamics.ValueProviders;

namespace Cyborg.Modules.Subprocess;

public sealed class EnvironmentVariableValueProvider() : DynamicValueProviderBase<EnvironmentVariable>("cyborg.types.env.var.v1");