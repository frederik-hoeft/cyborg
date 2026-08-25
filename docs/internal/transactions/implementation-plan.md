# Transaction Implementation Plan

> **Status:** Internal implementation plan for the target architecture in this directory.

## Approach

The migration should establish one correctness boundary at a time. Parallelism is intentionally late in the sequence: the runtime must first prove that ordinary sequential composition can repeatedly fork and join without losing state provenance or violating DI lifetimes.

Each stage should be independently reviewable, tested, and suitable for a local feature branch/patch before the next stage builds on it.

## Current-State Migration Boundaries

The current implementation contains several ownership assumptions that must change rather than be wrapped with transactional special cases:

| Current boundary | Target boundary |
|---|---|
| Deserialization constructs a worker and `ModuleReference` stores it. | Deserialization stores immutable module definition + generated activation identity; execution creates the worker. |
| Generated loader factory resolves worker dependencies from the load-time provider. | Generated activation resolves all invocation dependencies from the current module scope. |
| Root runtime and global environment are application-singleton workflow-state owners. | Each resolved root runtime is an independent execution session with its own logical global environment and runtime environment catalog. |
| Named-module registry is a singleton mutable workflow-state owner. | Static loading seeds immutable named-module state; runtime registration becomes transaction-local in Stage 4. |
| Runtime environment objects own mutable dictionaries and direct parent-object inheritance. | Environment views resolve a persistent transaction-owned environment graph by logical identity. |
| Nested module calls reuse runtime/provider objects without a mandatory module DI scope. | Every invocation creates and disposes a fresh DI scope tied to one transaction node. |
| Named-module deserialization mutates a runtime singleton registry. | Static loading builds immutable graph seed state; runtime dynamic registration is transactional. |
| Artifact publication can target another runtime/environment object directly. | Artifact targets resolve to logical environment identities inside the current transaction. |

These are architectural migrations, not compatibility shims. Temporary adapters are acceptable only when they preserve the target invariants and can be removed without changing the model.

## Stage 1: Immutable Loaded Definitions and Execution-Time Activation

### Goal

Remove invocation state and scope-sensitive dependencies from the deserialization boundary while preserving current sequential behavior.

### `Cyborg.Core` configuration/loading

- Separate loaded module definition identity from worker instances.
- Keep versioned loader lookup and JSON dispatch AOT-safe.
- Represent enough activation metadata in a loaded reference for later direct worker construction.
- Separate static named-module discovery from runtime registry mutation so loading produces immutable seed data.
- Keep module definitions immutable through generated preparation.

### `Cyborg.Core.Aot`

- Retarget generated worker construction to an execution-time activator role.
- Continue compile-time constructor analysis and direct construction.
- Resolve every non-definition constructor dependency from the provider supplied at activation time.
- Preserve generator diagnostics and native-AOT constraints.

### Validation gate

- The same loaded module definition can execute repeatedly with a fresh worker each time.
- Two concurrent activations of one loaded definition do not share worker fields.
- A scope-sensitive constructor probe comes from the provider used for activation, not the provider that deserialized the graph.
- Existing deserialization and source-generator tests remain green.

## Stage 2: Per-Invocation DI Scopes and Execution Sessions

### Goal

Make module invocation lifetime explicit before introducing transactional state mechanics.

### `Cyborg.Core` runtime execution

- Add the execution-session/root boundary.
- Centralize creation/disposal of one DI scope per module invocation.
- Resolve the DI scope factory through the service contract rather than type-checking a provider implementation.
- Bind a scoped execution context before activating workers or other scope-sensitive services.
- Ensure optional configuration-module execution nests inside the main `ModuleContext` invocation boundary.
- Remove silent unscoped execution fallbacks.

### `Cyborg.Cli`

- Compose process-wide application infrastructure separately from workflow execution sessions.
- Stop treating a singleton root runtime as the owner of mutable workflow state.

### `Cyborg.Core.TestAdapter`

- Mirror production scope semantics instead of relying on provider-specific behavior.
- Expose scoped/singleton probes suitable for nested-lifetime tests.

### Validation gate

- Root, child, grandchild, and sibling invocations each resolve different scoped probe instances.
- All invocations resolve the same ordinary singleton probe.
- Scope disposal occurs only after nested work owned by the invocation completes.
- Failure to create a required scope fails the invocation explicitly.

## Stage 3A: Transactional Key-Value Foundation

### Goal

Establish and independently validate the reusable map semantics used by transactional runtime components before transaction topology is introduced.

### `Cyborg.Core` transactional collections

- Represent one branch as an immutable persistent baseline plus a small mutable map of explicit final operations.
- Distinguish `Set(value)` from `Remove` so removals act as negative cache entries against the baseline.
- Preserve explicit modification provenance even when effective state returns to the baseline.
- Freeze effective state into structurally shared immutable snapshots without copying the complete dictionary at every fork.
- Allow multiple branches to derive from one exact frozen baseline.
- Prepare merged candidate state without mutating the owner, with deterministic same-key write conflict detection as the initial map policy.
- Keep the collection single-writer; concurrency is obtained by giving concurrent branches independent instances over the same immutable baseline.

### Validation gate

- `set -> baseline value` and `add -> remove` remain dirty.
- Removal hides a baseline value until explicitly set again.
- Frozen snapshots remain unchanged after later branch writes.
- Sibling branches share one baseline but cannot observe each other's changes.
- Non-overlapping branch changes produce a detached merge candidate while the owner remains unchanged.
- Set/set, set/remove, and remove/remove on one logical key conflict even when final values would compare equal.
- Repeated fork generations preserve earlier parent-relative provenance.

## Stage 3B: Transaction Core and Generic Participants

### Goal

Establish transaction topology and aggregate atomic reconciliation over the independently validated transactional state primitives.

### `Cyborg.Core` transaction subsystem

- Introduce parentless/root and child transaction nodes.
- Represent component state as immutable baseline + local changes.
- Add structurally shared snapshot materialization for map-like state.
- Introduce fork groups with one stable baseline, explicit continuation branch, and child branches.
- Make nested fork ownership structured and lifecycle-checked.
- Add the transactional participant registration/descriptor boundary.
- Implement prepare-then-publish reconciliation across the complete participant bundle.
- Add an extensible conflict-strategy boundary with deterministic fail-on-write-conflict as the first strategy.

### Synthetic test participants

Use simple purpose-built participants before migrating environments. They should make it easy to verify:

- local writes and removals;
- recursive change propagation;
- sibling conflicts;
- owner-continuation conflicts;
- multi-component atomic failure;
- independent roots.

### Validation gate

- Forking never clears existing owner provenance.
- A child joined before another child is forked still propagates its changes when the owner later joins upward.
- `set -> baseline value` and `add -> remove` remain dirty.
- Two siblings modifying the same logical key conflict regardless of final equality.
- If any participant fails preparation, no participant state changes in the owner.
- Completed/discarded transactions cannot be reused or joined twice.

## Stage 4: Transactional Workflow State

### Goal

Migrate every Cyborg-owned workflow-semantic state path onto the transaction core while preserving existing sequential behavior.

### Environment subsystem

- Replace mutable environment backing dictionaries with the logical environment graph described in [Environment and Runtime State](environment-and-runtime-state.md).
- Seed one logical global environment per root execution.
- Make named registration, topology, variable bindings, and transient reachability coherent component state.
- Make environment views transaction-bound rather than storage owners.
- Preserve existing resolution, override, interpolation, tagging, and decomposition semantics.

### Artifact publication

- Resolve every artifact scope through the current transaction's environment graph.
- Stage publication as environment changes so no child writes become caller-visible before join.

### Runtime named modules

- Seed root runtime registrations from the immutable loaded graph.
- Route dynamic registration/removal through a transaction-aware scoped facade.
- Preserve immutable loaded module definitions as registry values.

### Validation gate

- `Current`, `Parent`, `Global`, inherited, and named-reference environment access all obey the same fork isolation.
- A registration collision changes neither catalog nor topology.
- Artifact writes are invisible before join and visible after successful join.
- Child-local transient environments do not accumulate unnecessarily in ancestors.
- Dynamic named-module changes do not leak between siblings or roots.

## Stage 5: Sequential Compatibility

### Goal

Prove the new runtime is a drop-in state/lifetime model for existing composite execution before adding sibling concurrency.

Run the existing control-flow and configuration suite with focused regressions for:

- `Sequence`: early child writes remain visible to later children and propagate to the caller after sequence completion;
- `ForEach`: iteration-local and parent-targeted state follow existing environment rules;
- `While`: condition artifacts published to parent scope remain readable by the loop and compose across iterations;
- `If`/conditions: condition result publication remains visible at the expected scope;
- configuration modules: configuration changes join before main module preparation/validation;
- named references: repeated execution activates fresh workers and sees transaction-local registry state;
- interpolation/overrides: every lookup observes the correct transaction-bound environment snapshot.

This stage is a hard gate. `Parallel` should not be introduced while sequential execution still has transaction-specific regressions.

## Stage 6: `cyborg.modules.parallel.v1`

### Goal

Add the first multi-sibling structured-concurrency consumer without adding special state semantics to the module itself.

### `Cyborg.Modules`

- Add a non-empty collection of branch `ModuleContext` values.
- Open one fork group so all branches share exactly one baseline.
- Execute complete branch lifecycles concurrently through the core runtime.
- Await every started branch and preserve deterministic branch/result ordering.
- Reconcile all branch state in one aggregate join.
- Aggregate module statuses using established control-flow semantics.

### Shared-service audit

Parallel execution makes ordinary singletons concurrently reachable. Audit mutable singleton services used by module workers and make them thread-safe where necessary. Do not convert process-level concerns into transactional components unless their state has workflow semantics.

### Validation gate

- All branches observe the same fork baseline.
- Sibling and post-fork continuation changes are invisible before join.
- Non-overlapping writes reconcile regardless of task completion order.
- Same-key writes/removals fail deterministically and atomically.
- Nested `Parallel` preserves descendant provenance through the outer join.
- Caller cancellation reaches all branches and every started branch is observed before scope disposal.

## Stage 7: Final Documentation and Cleanup

Once the implementation and full suite are stable:

- reconcile `/docs/architecture` with the implemented steady-state runtime;
- update the module reference for `cyborg.modules.parallel.v1`;
- update README material only where the new capability changes user/contributor orientation;
- remove internal migration notes that no longer explain useful design constraints or tradeoffs;
- keep durable decision rationale where it helps future transaction consumers.

## Full-System Acceptance Matrix

The completed migration should include representative coverage across the following dimensions.

### Activation and DI

- concurrent repeated execution of one loaded definition with independent worker state;
- distinct scoped service identity at every invocation depth and parallel sibling;
- shared singleton identity across those invocations;
- all worker constructor dependencies resolved from the invocation scope.

### Transaction core

- parent pre-fork state visible to all children;
- parent/continuation post-fork writes invisible to children;
- sibling writes invisible before join;
- descendant changes remain dirty through later nested fork generations;
- explicit restoration remains dirty;
- multi-participant conflict leaves the complete owner state unchanged;
- multiple root sessions coexist independently.

### Environment/runtime state

- isolation through all environment scopes and named references;
- atomic catalog/topology updates;
- artifact isolation and publication;
- transient environment reachability/lifetime;
- dynamic named-module registration/removal isolation.

### Existing workflow behavior

- current control-flow, validation, configuration, interpolation, override, artifact, and named-module tests remain green;
- sequential workflows do not require module-specific transaction code.

### Parallel

- actual concurrent overlap is demonstrated rather than inferred;
- deterministic branch/result aggregation;
- deterministic write conflict semantics;
- nested parallel composition;
- cancellation and child-lifetime ownership.

## Deliberately Deferred Questions

The first migration should leave extension points, but does not need final product semantics for:

- result-driven commit/rollback policies;
- retry attempt selection and discard;
- managed background/sidecar continuation syntax;
- richer merge strategies;
- debugger branch-control UX;
- persistence/export of final root state.

Those consumers should fit the transaction and structured-execution model without changing its baseline/change, DI lifetime, or atomic reconciliation invariants.
