using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Model;
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
        static context => context.AddEmbeddedSource<LengthAttribute>(),
        static context => context.AddEmbeddedSource<MinLengthAttribute>(),
        static context => context.AddEmbeddedSource<MaxLengthAttribute>(),
        static context => context.AddEmbeddedSource<ExactLengthAttribute>(),
        static context => context.AddEmbeddedSource<DefinedEnumValueAttribute>(),
        static context => context.AddEmbeddedSource<DefaultInstanceAttribute>(),
    ];

    public static void Emit(IncrementalGeneratorPostInitializationContext context)
    {
        foreach (Action<IncrementalGeneratorPostInitializationContext> emitter in s_emitters)
        {
            emitter(context);
        }
    }
}
