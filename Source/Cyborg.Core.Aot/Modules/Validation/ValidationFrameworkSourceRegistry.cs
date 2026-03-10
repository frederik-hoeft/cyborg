using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class ValidationFrameworkSourceRegistry
{
    private static readonly ImmutableArray<Action<IncrementalGeneratorPostInitializationContext>> s_emitters =
    [
        static context => context.AddEmbeddedSource<GeneratedModuleValidationAttribute>(),
        static context => context.AddEmbeddedSource<RequiredAttribute>(),
        static context => context.AddEmbeddedSource<DefaultTimeSpanAttribute>(),
        static context => context.AddEmbeddedSource<IgnoreOverridesAttribute>(),
        static context => context.AddEmbeddedSource<ValidatableAttribute>(),
        static context => context.AddEmbeddedSource(typeof(DefaultValueAttribute<>)),
        static context => context.AddEmbeddedSource(typeof(RangeAttribute<>)),
        static context => context.AddEmbeddedSource(typeof(LengthAttribute)),
        static context => context.AddEmbeddedSource(typeof(MinLengthAttribute)),
        static context => context.AddEmbeddedSource(typeof(MaxLengthAttribute)),
        static context => context.AddEmbeddedSource(typeof(ExactLengthAttribute)),
    ];

    public static void Emit(IncrementalGeneratorPostInitializationContext context)
    {
        foreach (Action<IncrementalGeneratorPostInitializationContext> emitter in s_emitters)
        {
            emitter(context);
        }
    }
}
