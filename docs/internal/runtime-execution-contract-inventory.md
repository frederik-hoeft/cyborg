# Runtime Execution Contract Inventory (P0)

This is a baseline characterization for the [runtime execution and environment refactor](runtime-execution-environment-refactor.md), not a new steady-state architecture contract. It records which existing paths establish invocation and environment boundaries, and where regression tests protect the behavior before code is rearranged. The existing [system architecture](../architecture/architecture-overview.md), [transactions](../architecture/transactions.md) and [interpolation](../architecture/interpolation.md) documents remain authoritative.

## Execution entry points

`ModuleRuntimeBase` accepts five execution targets. All five are wrapped in a fresh child transaction, DI scope, and invocation by `ModuleInvocationCoordinator.ExecuteAsync`; their differences occur during dispatch **inside** that scope.

| Target | Current-scope path | Behavioral distinction |
| --- | --- | --- |
| `ModuleContext` | `ModuleContextRunner.ExecuteAsync` | Resolves requirements, runs an optional configuration module in a nested invocation, then dispatches the main reference in the owning invocation |
| Loaded configuration | `ModuleRegistry.ApplySeed` then context runner | Makes the loaded named-module seed visible before the context executes |
| Loaded root configuration | `ModuleRegistry.ApplySeed` then direct reference dispatch | Deliberately bypasses the context runner, including configuration and requirements |
| `ModuleReference` | Worker activation followed by worker dispatch | Activates a fresh worker from the reference within the invocation's DI scope |
| Already activated `IModuleWorker` | Direct worker dispatch | Skips reference activation but still creates the normal invocation boundary |

The call path is `IModuleRuntime`/engine entry point -> `ModuleExecutionRequest` -> `ModuleInvocationCoordinator` -> `ModuleInvocationScope` -> engine current-scope dispatch -> `ModuleDispatcher`. The execution-request objects call back into `IModuleExecutionRuntime` on the same runtime; this callback is an implementation detail, **not** an extra invocation boundary. Concurrent execution uses `ConcurrentExecutionScope` to create child invocation scopes under one open fork, with an owner continuation.

`ModuleExecutionLifecycleTests`, `ModuleRuntimeBaseTests`, `RootExecutionSessionTests` and `ConcurrentExecutionScopeTests` already characterize execution identity, scope boundaries, loaded-root execution, module-context configuration, lifecycle notification ordering, failures, rollback, cancellation, open/closed concurrent scopes, and detached/abandoned continuation cleanup. These tests should run unmodified throughout P1-P4.

## Environment selection and structural topology

`RuntimeEnvironmentContext` currently performs both execution-frame lookup and transactional graph access. Preserve the following distinctions when separating them:

1. `Current`, `Parent`, and `Global` select runtime views; they do not create logical nodes. `Current` and `Parent` clear the selected namespace to `__UNBOUND` before worker binding. `Parent` means the **parent invocation's environment**, not a logical environment's structural parent.
2. `Isolated` creates a new node without a structural parent. `InheritParent` and `InheritGlobal` create new nodes with an immutable reference to the selected parent view: logical ID, namespace, and override-resolution tags.
3. An unnamed new node is transient. A non-transient named node is registered transactionally; attempts to create a duplicate named registration fail without replacing it.
4. A named `Reference` first checks the active invocation/ancestor runtime views by **name**, then consults the transaction-visible registry. An active view retains its namespace and tags; registry reconstruction uses `__UNBOUND` for the newly selected node and reconstructs structural parents from their recorded descriptors.
5. The same logical environment ID can have many different contextual views. Distinct namespaces and override tags are not distinct transactional variable stores. A structural parent reference captures a **specific** view; ID equality alone cannot justify substituting another view during reconstruction.
6. A named environment created in a child transaction is invisible to sibling branches and to the frozen fork owner. It becomes visible after successful join; discarded branches must not publish registrations or bindings.

`RuntimeEnvironmentContextContractTests` captures these selection, view, reconstruction and transaction-rebinding invariants without needing a module dispatch. In particular, the inherited-named-environment tests compare creation-time views with registry reconstruction in the same transaction and across a fork. They deliberately assert baseline namespace/tag behavior, not the unimplemented provenance design.

The lower-level `TransactionalRuntimeEnvironmentTests` already cover duplicate registrations, transient pruning, retention of a transient ancestor referenced by a retained named environment, sibling collisions and binding conflicts, rollback publication policies, and module execution through registered environments.

## Transaction and invocation lifecycle

`ActiveTransaction` points an invocation at its writable transaction. An open fork **freezes the owner**; the continuation is the read/write path for work the owner performs concurrently with child branches. `RuntimeEnvironmentContext.RebindTransaction` recursively binds execution frames and structural parent views to the child or continuation transaction while preserving their namespace and override tags. It must not reconstruct parent topology from an unrelated runtime frame.

The normal definite-result order is `Started` -> `Completed` -> `Closed` after reconciliation. A failure before a definite module result still reports `Started` and `Closed`, without `Completed`; a conflicted join reports an unsuccessful close. Concurrent children may complete execution before the enclosing fork has joined, and therefore remain open until the scope is closed. Preserve observer delivery even when one observer fails, as characterized by `ModuleExecutionLifecycleTests`.

`ParallelModuleRuntimeTests`, `ConcurrentExecutionScopeTests`, `TransactionalServiceIntegrationTests`, and `TransactionalRuntimeEnvironmentTests` protect snapshot isolation, owner continuation, sibling conflict behavior, rollback policies, and atomic publication across transactional participants. Changes to invocation coordination must keep the same child-scope ownership and exception cleanup semantics.

## Refactor guardrails

The P0 tests intentionally do not require the proposed file-origin feature. Preserve the three existing independent identities: logical environment ID, contextual view binding, and selected transaction. A future definition-origin field belongs to the module reference, and an inherited parent may capture its durable contextual descriptor once those semantics are formally chosen. Do not simulate such capture by searching for a live invocation view that happens to have the same logical environment ID.

Before P1, there is no reason to rename public `IModuleRuntime` APIs or change the transaction participant implementation. First make selection and view materialization explicit, use this inventory as the behavioral checklist, and keep the existing variable expression machinery unchanged.

## Frame ownership after P2

The invocation chain is represented by `RuntimeInvocationFrame`, independently of `RuntimeEnvironmentContext` and the transactional graph. A frame records the current view, global view, and enclosing invocation frame. Its `ParentEnvironment` is therefore the enclosing **invocation** view, never `InheritedRuntimeEnvironment.Parent` (the **structural** parent of a logical environment).

`RuntimeEnvironmentContext` remains the compatibility entry point for `ModuleRuntimeBase` until the invocation coordinator is extracted. `CreateChildFrame` adds an execution frame using a view bound to the active transaction; `RebindTransaction` creates a corresponding invocation chain using a different `ActiveTransaction`, preserving namespaces, override tags, and the original ancestry. `BindToCurrentTransaction` binds an individual external view without adding a frame. `EnvironmentScopeSelector` works with frames directly, and `EnvironmentViewMaterializer` remains independent of invocation ancestry.

An `ActiveTransaction` is an invocation-owned mutable pointer, so a frame's bound views also follow `Retarget()` to the fork continuation without reconstruction or rebinding. A new child invocation instead receives a **different** active pointer and a copied frame chain. `RuntimeEnvironmentContextContractTests` exercises both cases, including the distinction between invocation and structural parents and the preservation of contextual bindings across a nested fork/join.

## Invocation lifecycle ownership after P3

`ModuleInvocationCoordinator` owns the normal child fork, continuation completion, scope creation, result-dependent transaction completion, reconciliation, and exceptional discard/teardown. `ModuleRuntimeBase` selects an execution request and hands it to the coordinator; it continues to implement the current-invocation dispatch API until P4. The coordinator uses the runtime's execution services and parent invocation identity to create the child DI scope and `ScopedRuntime`, then notifies `Started` **before** dispatch or environment preparation. `ModuleInvocationScope` owns the DI lifetime and idempotent `Closed` observation.

`ConcurrentExecutionScope` receives the same coordinator as a narrowly scoped collaborator, rather than a separate scope-creation delegate. It still owns its open fork, owner continuation and retarget/restore, child cancellation, atomic reconciliation, and final scope disposal. A concurrent child uses `ModuleInvocationCoordinator.CreateInvocationScopeAsync` to establish its invocation and emits `Completed` after a definite result; the enclosing concurrent scope delivers `Closed` only after reconciliation or abandonment. Sequential and concurrent execution therefore share scope construction and event setup, while retaining their distinct transaction-join policies.

## Dispatch ownership after P4

`ModuleRuntimeBase` remains the public `IModuleRuntime` facade; each execution overload constructs a data-only `ModuleExecutionTarget` and passes it to `ModuleInvocationCoordinator`. The coordinator owns the invocation transaction, DI scope, and lifecycle, then invokes `ScopedRuntime.DispatchInCurrentScopeAsync`. `ModuleTargetExecutor` owns dispatch inside that scope. Targets no longer call back through an internal runtime interface, and `ModuleExecutionRequest` and its five polymorphic subclasses have been removed.

`ModuleTargetExecutor` handles the five forms explicitly: normal context preparation, loaded-configuration registry seeding followed by context preparation, loaded-root registry seeding followed by direct reference execution, direct reference activation, and execution of an already activated worker. `ModuleContextPreparer` is limited to required-argument staging and optional configuration-child execution; it uses the public runtime API to start the configuration as a **new** invocation, then returns control to the executor for the primary reference in the existing invocation. Worker activation uses the invocation's DI scope, and worker namespace binding precedes worker preparation and hooks.

Concurrent children enter the same target executor through their established `ModuleInvocationScope`; they do not create a second invocation. The `IModuleExecutionRuntime` interface now retains invocation metadata (for debugging and tests) and the activated-worker invocation entry point required by the test adapter. It no longer exposes current-invocation dispatch operations or serves as an engine callback surface. Public `IModuleRuntime` and generator contracts are unchanged.

`ModuleExecutionLifecycleTests` now additionally cover loaded-seed visibility within the scoped worker, loaded-root bypass of requirements and configuration-child execution, and failed configuration short-circuiting the primary reference. Existing module-context, lifecycle, post-hook and concurrency tests cover the other execution forms and their boundaries.
