using Cyborg.Core.Modules.Configuration.Model;
using System.Collections.Immutable;

namespace Cyborg.Core.Configuration;

public sealed record ConfigurationSource(ImmutableArray<DynamicKeyValuePair> Options);