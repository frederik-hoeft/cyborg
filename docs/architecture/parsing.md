# Parsing Infrastructure

Grammar-based parser combinators for extracting structured data from borg subprocess output. Located in `Cyborg.Core/Parsing/`.

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Architecture Overview](#architecture-overview)
- [Core Components](#core-components)
- [Design Decisions](#design-decisions)
- [Creating Terminal Parsers](#creating-terminal-parsers)
- [Composing Parsers with Grammar Factory](#composing-parsers-with-grammar-factory)
- [Named Parsers for Parent Discrimination](#named-parsers-for-parent-discrimination)
- [Syntax Node Hierarchy](#syntax-node-hierarchy)
- [Visitor Pattern for Data Extraction](#visitor-pattern-for-data-extraction)
- [Example: Borg Prune Stats Grammar](#example-borg-prune-stats-grammar)
- [Integration with Subprocess Module](#integration-with-subprocess-module)

<!-- /code_chunk_output -->


## Architecture Overview

```
Grammar (static factory)
    │
    ├─ Grammar.Sequence(builder => ...)  → Sequence parser (all must match)
    ├─ Grammar.Alternative(builder => ...)→ Alternative parser (first match wins)
    └─ Grammar.Optional(builder => ...)  → Optional parser (always succeeds)

IParser<TSelf> (self-referential interface)
    │
    ├─ static TSelf.Instance            → Singleton accessor
    ├─ TryParse(input, offset)          → ISyntaxNode + charsConsumed
    └─ NamedCopy(name)                  → Named parser for parent discrimination
          │
RegexParserBase<TSelf> : IParser<TSelf>, IRegexOwner
    │
    ├─ static abstract Regex ParserRegex { get; }  ← Compiled via [GeneratedRegex]
    └─ abstract TryCreateSyntaxNode(match)         ← Create typed syntax node
          │
ISyntaxNode
    │
    ├─ Parent                           → Parent node reference
    ├─ HasParent(name)                  → Named parent traversal
    └─ Accept(INodeVisitor)             → Visitor pattern dispatch
          │
SyntaxNodeBase<TResult>
    │
    └─ abstract Evaluate()              → Extract typed result from node
```

## Core Components

| Component | Purpose |
|-----------|---------|
| `Grammar` | Static factory for building parsers via fluent builder API |
| `Sequence` | Combinator requiring all child parsers to match in order |
| `Alternative` | Combinator returning first successful child match |
| `Optional` | Wrapper that always succeeds (returns empty node if no match) |
| `RegexParserBase<TSelf>` | Abstract base for terminal parsers with compiled regex |
| `IRegexOwner` | Static abstract interface providing `ParserRegex` property |
| `ISyntaxNode` | AST node interface with parent linking and visitor support |
| `SyntaxNodeBase<TResult>` | Generic base class with `Evaluate()` for typed extraction |
| `INodeVisitor` | Marker interface for visitor pattern (consumers add `Visit(T)` methods) |

## Design Decisions

- **Static abstract interface** (`IRegexOwner`) ensures regex is compiled exactly once per parser type via `[GeneratedRegex]`
- **Zero-allocation pre-check** via `Regex.IsMatch(ReadOnlySpan<char>)` before executing full `Match()`
- **Curiously Recurring Template Pattern (CRTP)** (`RegexParserBase<TSelf>`) enables static `Instance` singletons
- **Parent-linked tree** enables `HasParent(name)` traversal for contextual discrimination
- **Named parsers** via `NamedCopy(name)` allow same parser type to have distinct parent identities

## Creating Terminal Parsers

Terminal parsers match text patterns via compiled regex. Use `RegexParserBase<TSelf>` with `IRegexOwner`:

```csharp
// 1. Define the syntax node (typed result container)
public sealed class ArchiveSizeSyntaxNode(long bytes) : SyntaxNodeBase<long>("ArchiveSize")
{
    public override long Evaluate() => bytes;
}

// 2. Define the terminal parser
internal sealed partial class ArchiveSizeParser 
    : RegexParserBase<ArchiveSizeParser>, IRegexOwner
{
    // [GeneratedRegex] compiles at build time (AOT-safe)
    [GeneratedRegex(@"\GOriginal size:\s*(?<bytes>\d+(?:\.\d+)?)\s*(?<unit>[KMGT]?B)")]
    public static partial Regex ParserRegex { get; }

    // Create syntax node from regex match
    protected override ISyntaxNode TryCreateSyntaxNode(Match match)
    {
        string bytesStr = match.Groups["bytes"].Value;
        string unit = match.Groups["unit"].Value;
        long bytes = ParseBytesWithUnit(decimal.Parse(bytesStr), unit);
        return new ArchiveSizeSyntaxNode(bytes);
    }

    private static long ParseBytesWithUnit(decimal value, string unit) => unit switch
    {
        "B" => (long)value,
        "KB" => (long)(value * 1024),
        "MB" => (long)(value * 1024 * 1024),
        "GB" => (long)(value * 1024 * 1024 * 1024),
        "TB" => (long)(value * 1024 * 1024 * 1024 * 1024),
        _ => throw new ArgumentException($"Unknown unit: {unit}")
    };
}
```

**Important**: The regex pattern MUST start with `\G` anchor to match only at the current offset (required by `Regex.IsMatch(ReadOnlySpan<char>)`).

## Composing Parsers with Grammar Factory

Use `Grammar.Sequence()`, `Grammar.Alternative()`, and `Grammar.Optional()` to build complex grammars:

```csharp
// Build a grammar for borg create --stats output
public static class BorgCreateStatsGrammar
{
    // Fluent builder API
    public static IParser CreateParser { get; } = Grammar.Sequence(seq => seq
        .Parser<HeaderLine>()              // "Archive name: backup-2024-..."
        .Parser<Whitespace>()
        .Parser<OriginalSizeLine>()        // "Original size: 1.23 GB"
        .Parser<Whitespace>()
        .Parser<CompressedSizeLine>()      // "Compressed size: 1.00 GB"
        .Parser<Whitespace>()
        .Parser<DeduplicatedSizeLine>()    // "Deduplicated size: 100 MB"
        .Optional(opt => opt               // Optional duration line
            .Sequence(inner => inner
                .Parser<Whitespace>()
                .Parser<DurationLine>()
            )
        )
    );

    // Alternative: Type-based composition (no builder, up to 8 parsers in sequences/alternatives)
    public static IParser CreateParserAlt { get; } = 
        Sequence<HeaderLine, Whitespace, OriginalSizeLine, Whitespace, 
                 CompressedSizeLine, Whitespace, DeduplicatedSizeLine>.Instance;
}
```

**Builder API vs Generic Types**:
- **Builder API** (`Grammar.Sequence(seq => ...)`) - Flexible, supports nesting, can name sub-parsers
- **Generic Types** (`Sequence<T1, T2, ...>.Instance`) - Zero-allocation singleton, up to 8 type params

## Named Parsers for Parent Discrimination

When the same parser type appears in multiple contexts, use named copies to distinguish them:

```csharp
// Without naming: Can't distinguish source vs destination size
IParser sizeGrammar = Grammar.Sequence(seq => seq
    .Parser<SizeParser>()       // Which one matched?
    .Parser<Whitespace>()
    .Parser<SizeParser>()       // Can't tell them apart!
);

// With naming: Parent name enables discrimination
IParser sizeGrammar = Grammar.Sequence(seq => seq
    .Parser(SizeParser.Instance.NamedCopy("source"))      // "source" parent
    .Parser<Whitespace>()
    .Parser(SizeParser.Instance.NamedCopy("destination")) // "destination" parent
);

// In visitor:
public void Visit(SizeSyntaxNode node)
{
    if (node.HasParent("source"))
        result.SourceSize = node.Evaluate();
    else if (node.HasParent("destination"))
        result.DestinationSize = node.Evaluate();
}
```

## Syntax Node Hierarchy

```
ISyntaxNode
    │
    ├─ SyntaxNodeBase (abstract, named, HasParent traversal)
    │   ├─ SequentialSyntaxNode  (Left + Right children)
    │   ├─ AlternativeSyntaxNode (Inner child wrapper)
    │   ├─ OptionalSyntaxNode    (Inner child or null)
    │   └─ WhitespaceSyntaxNode  (consumed char count)
    │
    └─ SyntaxNodeBase<TResult> (generic, adds Evaluate())
        └─ [User-defined typed nodes]
```

## Visitor Pattern for Data Extraction

Visitors traverse the AST to extract structured data:

```csharp
// Result container
public sealed class BorgCreateStats
{
    public string ArchiveName { get; set; } = "";
    public long OriginalSizeBytes { get; set; }
    public long CompressedSizeBytes { get; set; }
    public long DeduplicatedSizeBytes { get; set; }
    public TimeSpan? Duration { get; set; }
}

// Visitor implementation
public sealed class BorgCreateStatsVisitor(BorgCreateStats result) : INodeVisitor
{
    // Visit methods for each syntax node type of interest
    public void Visit(ArchiveNameSyntaxNode node) => 
        result.ArchiveName = node.Evaluate();

    public void Visit(OriginalSizeSyntaxNode node) => 
        result.OriginalSizeBytes = node.Evaluate();

    public void Visit(CompressedSizeSyntaxNode node) => 
        result.CompressedSizeBytes = node.Evaluate();

    public void Visit(DeduplicatedSizeSyntaxNode node) => 
        result.DeduplicatedSizeBytes = node.Evaluate();

    public void Visit(DurationSyntaxNode node) => 
        result.Duration = node.Evaluate();
}

// Usage
public static BorgCreateStats ParseBorgOutput(string output)
{
    if (!BorgCreateStatsGrammar.CreateParser.TryParse(output, 0, out ISyntaxNode? root, out _))
    {
        throw new FormatException("Failed to parse borg output");
    }
    
    BorgCreateStats stats = new();
    BorgCreateStatsVisitor visitor = new(stats);
    root.Accept(visitor);  // Traverses tree, calls Visit() for each node
    return stats;
}
```

**Note**: `INodeVisitor` is a marker interface. The `Accept()` implementation uses pattern matching or type checks to dispatch to visitor methods. Add `Visit(TNode)` methods for each node type you want to process.

## Example: Borg Prune Stats Grammar

```csharp
// Terminal parsers for prune output
internal sealed partial class PruneCountParser 
    : RegexParserBase<PruneCountParser>, IRegexOwner
{
    [GeneratedRegex(@"\GPruning (?<type>hourly|daily|weekly|monthly|yearly): (?<count>\d+)")]
    public static partial Regex ParserRegex { get; }

    protected override ISyntaxNode TryCreateSyntaxNode(Match match) =>
        new PruneStatSyntaxNode(
            match.Groups["type"].Value,
            int.Parse(match.Groups["count"].Value)
        );
}

internal sealed partial class DeletedArchivesParser 
    : RegexParserBase<DeletedArchivesParser>, IRegexOwner
{
    [GeneratedRegex(@"\GDeleted (?<count>\d+) archive\(s\)")]
    public static partial Regex ParserRegex { get; }

    protected override ISyntaxNode TryCreateSyntaxNode(Match match) =>
        new DeletedArchivesSyntaxNode(int.Parse(match.Groups["count"].Value));
}

// Grammar composition
public static class BorgPruneStatsGrammar
{
    public static IParser Parser { get; } = Grammar.Sequence(seq => seq
        .Optional(opt => opt.Parser<PruneCountParser>())  // Repeated per type
        .Parser<Whitespace>()
        .Parser<DeletedArchivesParser>()
    );
}
```

## Integration with Subprocess Module

The parsing infrastructure integrates with `SubprocessModuleWorker` for extracting metrics from borg commands:

```csharp
public sealed class BorgCreateModuleWorker(BorgCreateModule module)
    : ModuleWorker<BorgCreateModule>(module)
{
    public override async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        // Run borg create with --stats
        ProcessResult result = await RunBorgAsync(
            ["create", "--stats", Module.Repository, ...],
            cancellationToken
        );

        if (result.ExitCode != 0)
            return false;

        // Parse stats from stdout
        if (BorgCreateStatsGrammar.Parser.TryParse(result.StdOut, 0, out ISyntaxNode? node, out _))
        {
            BorgCreateStats stats = new();
            node.Accept(new BorgCreateStatsVisitor(stats));
            
            // Export as Prometheus metrics
            MetricsBuilder.AddGauge("cyborg_backup_original_bytes", stats.OriginalSizeBytes);
            MetricsBuilder.AddGauge("cyborg_backup_compressed_bytes", stats.CompressedSizeBytes);
            MetricsBuilder.AddGauge("cyborg_backup_deduplicated_bytes", stats.DeduplicatedSizeBytes);
        }

        return true;
    }
}
```
