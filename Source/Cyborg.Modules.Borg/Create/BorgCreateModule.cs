using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Validation;
using Cyborg.Core.Parsing.Grammars;
using Cyborg.Core.Parsing.Parsers;
using Cyborg.Modules.Borg.Create.InputValidation;

namespace Cyborg.Modules.Borg.Create;

[GeneratedModuleValidation]
public sealed partial record BorgCreateModule
(
    [property: Required] string ArchiveName,
    [property: Required][property: DirectoryExists] string SourcePath,
    [property: Required][property: DefaultValue<string>("lz4")][property: MatchesGrammar(nameof(BorgCreateModule.CompressionGrammar))] string Compression,
    [property: Required][property: DefaultInstance] BorgExcludeOptions Exclude
) : BorgModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.borg.create.v1.4";

    private static IParser CompressionGrammar
    {
        get
        {
            if (field is not null)
            {
                return field;
            }
            IParser sep = new Literal(",");
            IParser methods = Grammar.Alternative(builder => builder
                .Parser(new Literal("none"))
                .Parser(new Literal("lz4"))
                .Sequence(seq => seq
                    .Parser(new Literal("zstd"))
                    .Optional(opt => opt.Sequence(seq => seq.Parser(sep).Parser(new Number(min: 1, max: 22)))))
                .Sequence(seq => seq
                    .Parser(new Literal("zlib"))
                    .Optional(opt => opt.Sequence(seq => seq.Parser(sep).Parser(new Number(min: 0, max: 9)))))
                .Sequence(seq => seq
                    .Parser(new Literal("lzma"))
                    .Optional(opt => opt.Sequence(seq => seq.Parser(sep).Parser(new Number(min: 0, max: 9))))));

            IParser grammar = Grammar.Alternative(builder => builder
                .Parser(methods)
                .Sequence(seq => seq
                    .Parser(new Literal("auto"))
                    .Parser(sep)
                    .Parser(methods)));
            return field = grammar;
        }
    }
}

[Validatable]
public sealed record BorgExcludeOptions
(
    bool Caches,
    [property: Required] IReadOnlyCollection<string> Paths
) : IDefaultInstance<BorgExcludeOptions>
{
    public static BorgExcludeOptions Default { get; } = new(Caches: false, Paths: []);
}