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

Generated validation retains its established ordering: defaults, overrides, defaults after selection, typed resolution, destination invariants, interpolation, and constraints. A `StructuralModulePreparation` instance belongs to a single validation invocation. It tracks selected bound overrides and pending expressions by stable logical member paths across nested record and collection reconstructions. The typed phase may materialize CLR values for destination invariants and marks those results terminal, preventing a subsequent interpolation phase from reinterpreting their characters. Worker-facing module records are ordinary CLR objects and carry no expression nodes.

The environment exposes explicit `Interpolate(string)` as a fresh textual input boundary; worker-controlled interpolation intentionally starts another pass. Neither that API nor public enumeration is a reason to reparse a bound internal expression.

## Legacy CLR compatibility boundary

`IRuntimeEnvironment` retains internal raw-string selection and value-expression methods for compatible implementations that do not expose Cyborg's structural environment store. These signatures can carry only CLR strings, not terminality metadata. They therefore still use a limited `ExpressionShield` bridge for captured textual values passed to additional legacy preparation stages. Normal generated preparation against `RuntimeEnvironment` bypasses these APIs and retains structural state.

Eliminating the last bridge requires a separate compatibility decision about the raw-string interface contract and custom `IRuntimeEnvironment` implementations. Silently deleting escape protection from those legacy methods would make captured syntax active again and change behavior. This limitation is deliberately explicit rather than hidden behind a new textual expression model.
