# Structural Variable Expressions: Design and Migration

## Scope and architectural boundaries

Cyborg's variable language currently interprets textual values through regex fragments during storage, recursive resolution, and generated module preparation. Internal finality is also conveyed by adding escape markers to rendered strings. The target architecture represents these semantics as structured values so that an expression cannot inadvertently become active merely because an intermediate consumer needs a CLR string.

The migration retains the existing grammar, evaluation timing, environment inheritance, entry-point resolution, tagging, module validation ordering, public CLR-facing contracts, and transaction semantics. This document records the design boundaries and the intended integration sequence. It is not a second steady-state reference for the current runtime while the migration is in progress.

The expression lifecycle has four distinct representations, plus a transient parser tree:

1. **Syntax:** the parser constructs a parse tree using `Cyborg.Core.Parsing`; a syntax collector produces an immutable expression AST. Syntax describes literals, references, operators, and escape depth, but contains no resolved environment values.
2. **Bound:** binding takes a syntax value and an applicable environment view. Eager captures resolve at their definition boundary, lazy references retain their reference and the required resolution-origin semantics, and ordinary CLR objects become terminal bound values. Every environment binding uses the same structural value contract; it must not depend on guessing whether an arbitrary `object` is an expression type.
3. **Evaluated:** evaluating a bound value against the appropriate *current* resolution context yields a terminal immutable structure for that evaluation. Evaluation never mutates the stored bound value. Nested interpolation, tagged text, unresolved placeholders, and escapes are represented as structural results, not text to be parsed again. A terminal result need not imply that every textual reference was found; unresolved `${...}` is preserved according to the existing language contract.
4. **Materialized:** only explicit CLR boundaries convert terminal evaluated results to `string`, `TaggedString`, or other CLR objects. Captured and nontext CLR objects preserve their existing reference identity; immutable wrappers do not promise deep immutability of contained user objects.

A parser-framework syntax node has mutable parent links and optional visitor behavior. These nodes are *not* the long-lived environment expression model. The compiler collects an independent immutable syntax AST; later phases should likewise return distinct immutable bound and evaluated value types. No syntax/bound/evaluated type should double as a CLR materialization result.

## Grammar and phase 1

The first phase defines only the parser and immutable syntax model. It deliberately does not change `EnvironmentLike`, `VariableSyntaxBuilder`, generated preparation, or existing runtime behavior. The existing public resolver and generator remain authoritative until later phases are integrated.

The grammar consists of specialized parsers for textual interpolation, escaped tokens, typed indirection, capture, and variable references. Self-contained, source-generated regex parsers recognize reference identifiers, escaped tokens, and active typed fragments; composed parsers recognize complete interpolation, indirection, and capture forms without duplicating those token patterns. A literal-run parser consumes characters only until the next recognized token from the same combinator grammar; it does not maintain another independent list of operator patterns. A grammar coordinator selects whole-value typed forms or text, and a visitor collects the textual segments into immutable syntax. Supporting additional operators should require introducing and composing the corresponding specialized parser and node rather than editing a monolithic brace scanner.

The source language is unchanged:

| Source | Parsed form |
|---|---|
| `plain`, `prefix ${name}` | Text containing literal and interpolation segments |
| `${@name}`, `${@}`, `${@@}` | Entry-point reference, current namespace, entry-point namespace |
| `&{name}`, `&{@name}`, `&{@}`, `&{@@}` | Whole-value lazy typed reference |
| `*{name}` | Whole-value eager capture |
| `${#name}`, `&{##name}`, `*{#@name}` | Escaped token preserving its operator, hash count, and uninterpreted body |

Active `&{...}` or `*{...}` fragments outside a valid whole-value typed form are rejected. `*{@...}` remains reserved. An invalid interpolation candidate remains literal as before; escape layers are never removed during parsing. Identifier syntax retains its existing name and dotted-segment rules. Raw CLR objects are represented by terminal syntax nodes without attempting to parse their contents.

The grammar compiler is not the evaluator, nor a public string-interpolation API. Later phases must not use a rendering of this syntax to round-trip between internal phases.

## Binding and evaluation contracts (phase 2)

Binding must occur when the effective value and runtime context are known, not necessarily when configuration is deserialized. `SetVariable` supplies that boundary for an environment binding; module defaults and overrides are selected before the corresponding runtime binding. An eager `*{...}` capture is evaluated once at that boundary. An `&{...}` or `${...}` remains lazy and must resolve against the appropriate current view on every evaluation. Entry-point-modified references retain the original requesting environment when traversing ancestors; unmodified references follow the scope where the expression is encountered.

Terminal evaluation results carry enough structure to prevent a captured literal containing `${...}` from becoming a fresh expression in another phase. Layered `#` escapes consume one layer per applicable evaluation pass without rescanning the newly revealed text in that same pass. Tags propagate when textual interpolation incorporates tagged values; typed indirection and capture preserve the target's CLR kind and tags. Missing text references remain literal, while missing typed references and cycles retain the current errors.

An evaluator may use an explicit per-call context object, scoped services, or polymorphic node behavior. The extension boundary should be local to the added expression form, rather than requiring coordinated edits to a set of centralized node-type switches. Per-evaluation state (such as recursion tracking and resolution scope) must not be stored mutably in shared bound expression nodes.

## Runtime and generated preparation (phase 3)

Environment storage, inheritance, named-environment reuse, publication, and transaction forks should share immutable *bound* values internally. Copying or forking must not materialize and then reparse these bindings. Public enumeration and public CLR resolution remain distinct from internal structural transfer.

Module preparation must preserve the current sequence: defaults, override selection, defaults after selection, typed-value resolution, destination invariants, interpolation, then constraints. The implementation can change generated traversals or use an internal preparation-state owner, but must keep expression state intact through nested `[Validatable]` records, collections, nullability handling, and multiple executions of the same module definition. Module implementations still consume ordinary CLR properties. `[IgnoreInterpolation]`, `[IgnoreValueExpression]`, and `[Secret]` remain effective on the selected value, not merely the original configuration text.

Explicit `Interpolate(string)` and intentionally deferred worker interpolation are legitimate *new input* parsing boundaries. They are not justification for reparsing an already compiled internal expression or inserting escape hashes between generated preparation phases.

## Consolidation and review gates

- **Phase 1 review:** parser/AST only, syntax characterization tests, compile/parse contract, and this design; no runtime change.
- **Phase 2 review:** independent bound and evaluated models, eager/lazy correctness, finalization without text shielding, terminal materialization and tags.
- **Phase 3 review:** environment stores, module-generation integration, nested values, scope timing, publication and transactional behavior, production tests and AOT constraints.
- **Phase 4 review:** remove superseded regex grammar and shielding paths, remove duplicate representations, and reconcile `/docs/architecture` to describe the implemented steady state. Preserve just enough internal rationale for future changes without duplicating steady-state documentation.

Compatibility tests should check both the ordinary semantic path and transformations across boundaries: a captured literal containing active-looking syntax remains inert throughout module preparation; a lazy reference remains live through the same path and across forks; escaped text consumes exactly one layer; source CLR text kind and secret tags remain intact.

## Bound/evaluated engine (phase 2)

The phase-2 engine implements the four-representation separation without replacing the existing environment resolver yet. It establishes the contract the phase-3 runtime integration will consume:

- Each immutable syntax node binds polymorphically into a separate immutable bound value or text-part type. A plain CLR object is wrapped in `BoundTerminal`; a capture becomes `BoundCapture` holding a terminal evaluation snapshot. The latter is created at binding, not at syntax compilation or first retrieval.
- Lazy typed references and textual interpolations retain only a structural `VariableReferenceSyntax`. They do not retain a concrete environment instance. Evaluation receives `IExpressionResolutionContext`, which interprets the current and entry-point anchors, resolves against the appropriate scope, and will own cycle detection in the runtime adapter. Recursive references are returned as *evaluated terminal structures*, not strings needing another parse pass.
- The evaluated model is independent of both the syntax and bound types. Evaluated text comprises terminal literal and already-evaluated splice segments. Escaped operator tokens lose exactly one structural escape layer when evaluated and cannot become an active expression during materialization. A missing textual reference yields terminal literal placeholder text for that evaluation only; the bound reference remains live for subsequent evaluations.
- The separate `ExpressionMaterializer` converts an evaluated structure to its public CLR value. Tagged input text preserves its CLR text kind even with an empty tag set; resolved splice tags union into the output; typed references and captures retain non-text CLR object identity. Immutability applies to expression wrappers and composite structure, not arbitrary user CLR objects.

The parser's `ActiveTypedFragmentParser` is deliberately a *rejection-only* recognizer used in textual contexts. The top-level grammar already composes `TypedReferenceParser` and `CaptureParser` as alternatives to classify valid whole-value expressions. The rejection parser also needs to catch malformed active tokens (`&{}` or `*{1bad}`), which those valid parsers cannot recognize, and therefore has no reason to return distinct capture/indirection syntax nodes. Its generic detector is not a second evaluator grammar.

**Phase 2 review boundary:** these models and their focused tests are additive. The existing `EnvironmentLike`, generated module preparation, and `ExpressionShield` remain untouched and authoritative until phase 3. The new engine never calls them. Phase 3 must supply an adapter for resolution context and must not expose intermediate CLR strings as a transport between preparation stages.
