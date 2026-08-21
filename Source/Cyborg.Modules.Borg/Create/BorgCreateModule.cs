using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Parsing.Grammars;
using Cyborg.Core.Parsing.Parsers;
using Cyborg.Modules.Borg.Create.InputValidation;
using Cyborg.Modules.Borg.Create.Model;

namespace Cyborg.Modules.Borg.Create;

[GeneratedModuleValidation]
public sealed partial record BorgCreateModule
(
    [property: Required][property: Untagged] string ArchiveName,
    [property: Required][property: Untagged][property: DirectoryExists] string SourcePath,
    [property: Required][property: Untagged][property: DefaultValue<string>("lz4")][property: MatchesGrammar(nameof(BorgCreateModule.CompressionGrammar))] string Compression,
    [property: Required][property: DefaultInstance] BorgExcludeOptions Exclude,
    [property: Required][property: DefaultInstance] BorgFilesCacheSentinelOptions FilesCacheSentinel
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
