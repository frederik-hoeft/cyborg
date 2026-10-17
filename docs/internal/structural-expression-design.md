# Structural Expression Design Decisions

The steady-state expression language and public resolution semantics are documented in [Interpolation and Override Resolution](../architecture/interpolation.md). This record explains the internal representation boundaries and the compatibility trade-offs behind the implementation, rather than duplicating the language specification.

## Separate expression representations

A source value passes through distinct stages:

1. **Syntax:** specialized `Cyborg.Core.Parsing` combinators recognize operators, references, escaping, and literal runs. Parser syntax nodes are transient and may carry mutable parent links. The grammar collects an independent immutable `ValueSyntax` tree.
2. **Bound:** binding transforms syntax into immutable `BoundValue` nodes. Eager capture resolves at the effective definition boundary, while lazy nodes retain reference targets rather than a mutable environment object. Ordinary CLR values are wrapped as terminal bound values, so the environment store has one unambiguous internal value contract.
3. **Evaluated:** each read evaluates against its current resolution view, producing independent immutable `EvaluatedValue` nodes. Escapes and interpolated/spliced terminal text are represented structurally; a newly revealed operator is never reinterpreted during the same pass. Evaluation does not modify stored bound values.
4. **Materialized:** `ExpressionMaterializer` exposes `string`, `TaggedString`, and other CLR values to consumers. Only the structural wrappers are immutable: captures of arbitrary CLR objects retain the identity of the resolved object without deep-cloning it.

Grammar operators have dedicated parsers and node types; new forms should be added by extending these compositions and their node-specific binding/evaluation behavior, not by duplicating the grammar in independent regex fragments or centralized type switches. Self-contained generated regexes are appropriate for lexical tokens, including variable references and escaped tokens. Ordinary identifier validation reuses the reference grammar.

## Runtime and generated preparation

`SetVariable` compiles and binds once. The environment stores bound nodes, and recursion uses the calling entry point and the current resolution scope, without storing mutable resolution state on shared nodes. Internal environment copies, transaction seeding, and publication between compatible environments transfer the original bound values; public variable enumeration presents their historical CLR-facing stored form and is not used for structural transfer.

Generated validation retains its established ordering: defaults, overrides, defaults after selection, typed resolution, destination invariants, interpolation, and constraints. A `StructuralModulePreparation` instance belongs to a single validation invocation. It tracks selected bound overrides and pending expressions by stable logical member paths across nested record and collection reconstructions. The typed phase may materialize CLR values for destination invariants and marks those results terminal, preventing a subsequent interpolation phase from reinterpreting their characters.

Generated defaults that replace a selected textual override discard its retained expression state, so later phases cannot restore a superseded value. Eager captures freeze the materialized text of interpolated results at binding time, while direct captures of CLR objects retain shallow identity. Worker-facing module records are ordinary CLR objects and carry no expression nodes.

The environment exposes explicit `Interpolate(string)` as a fresh textual input boundary; worker-controlled interpolation intentionally starts another pass. Neither that API nor public enumeration is a reason to reparse a bound internal expression.

## Generated preparation boundary

Generated validation uses `ModuleValidationContext` to translate ordinary CLR module properties into per-invocation structural preparation state. The supported runtime environment implementations share the `RuntimeEnvironment` storage model, so the context requires that representation rather than falling back to older raw-string APIs. Textual overrides remain selected independently of evaluation, and their bound values are retained across record rewrites without adding escape characters. Public `Interpolate(string)` remains a separate, explicit textual input boundary.
