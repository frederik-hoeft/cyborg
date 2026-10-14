# Module Definition Provenance: Design and Integration

This document records the architectural decisions for definition-file provenance and the boundaries used to integrate it into the execution and environment model. The steady-state contracts belong in [System Architecture](../architecture/architecture-overview.md), [Interpolation](../architecture/interpolation.md), and [Transactions](../architecture/transactions.md); this document focuses on the alternatives, invariants, and review considerations relevant to the feature.

## Problem and dimensions

Relative paths in module configuration commonly need to be relative to the file that defines the module, not the process working directory or the file that happened to invoke it. A module may be loaded once, passed as data, invoked repeatedly, and execute in a named environment created by another module. None of the following identities can stand in for the others:

| Dimension | Owns | Does not determine |
| --- | --- | --- |
| Definition identity | Immutable `ModuleReference` and its source-file origin | Where the reference is invoked or which environment it accesses |
| Invocation hierarchy | Active execution identity, DI scope, lifecycle, nested work | Structural environment parentage |
| Logical environment graph | Environment nodes, named registrations, parent-view descriptors | Which module created or currently uses a logical environment |
| Transaction branch | Which graph nodes/variables/registrations are visible and may reconcile | Module definition origin or contextual view identity |

An environment **view** connects the latter dimensions: it identifies a logical node, the selected transaction, and the contextual bindings used to interpret its variables. Several differently bound views can address the same logical environment. The CLR view is disposable runtime state, but its *descriptor* can be captured when a structural inheritance relationship must outlive that view.

## Decision: immutable definition metadata plus contextual views

A `ModuleOrigin` contains the absolute definition-file path and its containing directory. A `ModuleReference` owns an optional origin; constructing one in code without an origin leaves the contextual properties undefined. No global origin is inferred. The loader uses the trust-approved file path, normalizes it to an absolute path, and supplies it to a **single deserialization operation**. The operation clones JSON serializer options and installs a converter with that origin. Nested `ModuleReference` instances, including references parsed through dynamic-value providers, therefore receive the same definition-file origin.

Independent file loads have independent converter state; no ambient `AsyncLocal` or static current-file scope exists. An `ExternalModule` that loads another file creates a new load operation, so the target module references carry that other file's origin. Module references passed via the runtime named-module registry, dynamic modules, or environment values retain their immutable provenance. The test adapter can also supply an explicit origin for in-memory JSON.

At invocation dispatch, the module's reference supplies a new `EnvironmentContext` binding to the selected runtime environment view *before* context requirements, optional configuration-child dispatch, worker preparation, and execution. A child invocation binds its own reference. Executing an already activated worker, which has no `ModuleReference`, explicitly binds an empty definition context instead of accidentally inheriting the caller's origin. Structured invocation lifecycle hooks have the module's immutable `Origin` on their lifecycle context, even for `Started` notifications that occur before worker environment binding.

`EnvironmentContext` is an immutable descriptor, independent of transactional user-variable storage. It owns contextual variable resolution for bare `@` (the view namespace) and `@.origin.file` / `@.origin.directory`; future definition-stable contextual values can extend the descriptor and resolver. The names are runtime-owned, cannot be written or removed as user variables, and an undefined contextual name is **consumed** rather than allowed to fall back to a structural parent.

## Decision: capture the structural parent's view descriptor

The logical environment graph already records a parent-view descriptor (parent ID, namespace, override tags). `InheritParent` and `InheritGlobal` additionally capture the immutable contextual descriptor of the selected parent. A later named-environment lookup reconstructs the same parent view from transaction-visible topology, without searching active invocation frames. This is intentionally *captured structural inheritance*, similar to a closure, rather than a new global provenance graph or dynamic call-stack scoping.

The newly executing module always binds **its own** context to the selected environment node. Captured context applies only to that node's inherited structural parents. Rebinding an environment to another transaction preserves those view descriptors. Committing a named environment may retain a captured immutable descriptor as part of its topology, but does not write contextual values into its parent or sibling variable stores. A rolled-back environment creation cannot publish its descriptor. Different named environments can capture different views of the same logical parent without changing that parent's transactional state.

### Resolution examples

Suppose A is defined by `a.jconf`, runs in global environment G, and creates named S using `InheritParent`. Later B from `b.jconf` executes in S:

| Expression and location | Meaning |
| --- | --- |
| `${@.origin.file}` directly in B's view of S | B's defining file |
| `${@.origin.file}` encountered in a lazy value stored in inherited G | A's captured defining file |
| `${@@.origin.file}` encountered while resolving that inherited value from B's S view | B's defining file (original lookup entry point) |
| `&{@.origin.file}` in inherited G | Lazy typed reference resolved in G's captured view |
| `*{@.origin.file}` assigned in G | Immediate snapshot of G's contextual origin; remains unchanged across subsequent rebinding |

A lazy variable does **not** remember which module last wrote its textual contents. Its contextual references resolve in the environment view where the text is evaluated; `*{...}` is the explicit mechanism for assignment-time capture. This preserves the existing distinction between late-bound expressions and snapshots, avoiding a second persistent writer-provenance graph.

For a view with no origin, `${@.origin.file}` remains an unresolved placeholder; `&{@.origin.file}` and `*{@.origin.file}` fail with the existing undefined-reference behavior. In particular, an originless child must not silently resolve its parent's origin. The `@@` variant targets the original entry-point view, even when a stored value is read from an ancestor.

## Considered alternatives

**Ambient origin during JSON loading.** Passing origin through an `AsyncLocal` makes independently initiated nested deserializations inherit the wrong origin and couples converter behavior to execution-context propagation. Per-load serializer options already form an explicit propagation channel and are preferable.

**Origin stored as environment variables or keyed only by environment ID.** Both break module identity: different module definitions can execute in the same environment and the same module can execute in many environments. Publishing origin as a user variable would also leak across transaction reconciliation and allow spoofing.

**Recover parent provenance from live frames.** Logical environment IDs do not uniquely identify contextual views. An execution-frame lookup can substitute the wrong namespace, override tags, and origin, or find no matching view after the defining invocation has completed. A structural parent view must reconstruct from its captured descriptor.

**Invocation-only provenance through structural inheritance.** This is a defensible alternative but conflicts with the existing capture semantics for parent namespaces and override-resolution tags. It also makes inherited `@.origin.*` values depend on the current invocation stack unless an explicit dynamic-context parameter is threaded through every resolution path. Captured descriptors are simpler and preserve established environment-view behavior.

## Implementation boundaries and review checklist

1. Loader and JSON converters: verify file normalization, per-load options isolation, nested/module-as-data references, and separate origins for external loads.
2. Resolution grammar: retain existing `@` / `@@`, override addressing (`@module.property`), and normal identifier semantics; add contextual paths to interpolation and lazy indirection. Allow eager capture of the explicit origin paths without enabling arbitrary `*{@identifier}` capture.
3. Execution binding: install reference origin before module-context preparation, worker validation/preparation, execution and post-worker hooks. A direct activated-worker invocation has no implicit origin.
4. Environment topology: snapshot contextual parent bindings upon inherited node creation/import; retain them on materialization and transaction rebinding without publishing ordinary variable bindings.
5. Lifecycle: expose definition origin explicitly on structured lifecycle contexts because the start event intentionally precedes worker environment setup. Do not change lifecycle notification order.
6. Coverage: contextual resolution from nested inherited views, missing provenance and no parent fallback, eager capture and lazy entry-point references, named environment reconstruction, programmatic references, nested file deserialization, module-as-data, independent file loads, and external target origin.

No source-generated module validation or serializer metadata shape is changed; `ModuleOrigin` is runtime metadata deliberately ignored by JSON serialization of module references. There is no new process-global provenance state and no change to transaction participant reconciliation or conflict selection.
