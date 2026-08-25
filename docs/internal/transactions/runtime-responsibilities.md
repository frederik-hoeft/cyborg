# Runtime Responsibility Boundaries

> **Status:** Internal pre-transaction refactoring design. This document describes the intended responsibility split used as preparation for the transactional runtime architecture in this directory.

## Purpose

The runtime is the consumer-facing execution context for module workers. It should remain the single facade through which workers access their current environment, execute nested modules, prepare child environments, and complete execution. Those capabilities do not require the facade itself to own every implementation mechanism.

The pre-transaction runtime has accumulated several independent responsibilities in `ModuleRuntimeBase` and the environment types. Separating those responsibilities before transaction state is introduced reduces the number of concerns that must change simultaneously when execution scopes and persistent state replace the current mutable runtime hierarchy.

## Consumer-Facing Runtime

`IModuleRuntime` remains the module-facing API. Module workers should not need to inject low-level execution, environment-catalog, transaction, or artifact-publication services for ordinary work.

The runtime facade owns the behavioral contract, not the backing mechanisms. Its core responsibilities are:

- expose the current, parent, and logical-global environment views;
- execute a `ModuleContext` in an explicitly selected environment;
- execute a loaded `ModuleReference` in an explicitly selected environment;
- prepare an environment from a `ModuleEnvironment` description;
- resolve configured environment references;
- complete a module result and publish its artifacts according to module policy.

Overloads that only choose a default environment or translate `EnvironmentScope`/`ModuleContext` metadata into one of those core operations are convenience syntax and belong in runtime extension methods. Environment catalog mutation is runtime infrastructure and is not part of the consumer contract.

This distinction becomes more important once every invocation owns a DI scope and transaction node: the public facade can remain stable while its backing execution/session state changes substantially.

Internal runtime collaborators should depend on narrow internal capability interfaces rather than concrete runtime implementations when they need behavior beyond `IModuleRuntime`. Concrete runtime types should only be coupled directly where they form an intentionally inseparable implementation pair, such as a private or internal scope-bound runtime created by the runtime facade itself.

## Environment Scope and Catalog

The runtime hierarchy currently needs two related but distinct environment concepts:

1. **runtime-relative environment context**: current, parent, and logical-global environment relationships plus environment creation/reference resolution;
2. **named environment catalog**: registration and lookup of non-transient named environments.

These concerns belong in dedicated runtime environment objects rather than in `RootModuleRuntime`/`ScopedRuntime` implementations. Root and scoped runtimes should differ primarily in execution ancestry, not in duplicated environment bookkeeping.

The runtime environment context is intentionally internal. Modules continue to use `IModuleRuntime` and `IRuntimeEnvironment`; they do not consume a catalog service directly.

This split is transitional in representation but durable in responsibility. The mutable catalog and object-reference hierarchy will later be replaced by the transaction-owned logical environment graph described in [Environment and Runtime State](environment-and-runtime-state.md). The runtime facade should not need another public redesign when that happens.

## Module Context Execution

Executing a `ModuleContext` is orchestration above raw worker dispatch. It consists of:

1. resolving and importing required arguments into the selected environment;
2. executing the optional configuration module in that environment;
3. stopping when configuration fails or is canceled;
4. executing the main loaded module reference.

This is one cohesive responsibility and should be isolated from raw worker dispatch. It is also the boundary that later owns the main invocation transaction while configuration execution becomes a nested child invocation.

## Worker Dispatch and Lifecycle

Raw worker dispatch owns execution mechanics after a module definition has already been selected and an environment has already been prepared:

- activate a fresh worker from the current execution service provider;
- invoke the worker against its scoped runtime;
- translate cancellation and unhandled exceptions into execution results;
- emit lifecycle logging;
- run post-execution hooks without allowing hook failure to replace the module result.

This mechanism is internal runtime infrastructure. It must resolve scope-sensitive services from the current invocation provider when per-invocation DI scopes are introduced, but module consumers do not need direct access to it.

Keeping activation/dispatch separate from `ModuleContext` orchestration also makes the future scope boundary explicit: the invocation scope must exist before worker activation and remains valid through post-execution hooks.

## Artifact Publication

`Exit` is part of the module-facing runtime contract because it converts a worker result into the externally visible completed result and applies configured artifact-publication semantics. The publication mechanics are nevertheless a distinct policy responsibility.

Artifact publication determines the responsible runtime-relative environment target, builds the artifact collection, publishes it, and returns the normalized execution result. Isolating that policy makes the transaction migration straightforward: publication can later resolve logical environment identities and stage writes in the current transaction without changing worker APIs.

## Runtime Environment Internals

`EnvironmentLike` and `RuntimeEnvironment` contain two layers that should be kept conceptually separate:

- **environment state storage**: bindings and later transaction-local changes;
- **environment semantics**: resolution, interpolation, indirection, overrides, decomposition, namespaces, and inherited lookup.

The mutable binding dictionary is therefore hidden behind a small storage boundary. The current implementation remains mutable and preserves existing shared-binding behavior for bound environment views, but resolution logic no longer depends directly on `Dictionary<string, object?>` as its storage contract.

The transaction migration should replace that storage boundary with transaction-owned persistent baseline/change state rather than attempting to make the current dictionary concurrent.

Module namespace calculation and bulk publication are convenience operations rather than intrinsic mutable environment state. They can remain extension-level behavior while the core `IRuntimeEnvironment` contract focuses on resolution, binding, override state, and artifact/decomposition semantics.

## What Should Not Become Consumer DI

The refactor deliberately does **not** introduce module-facing services such as `IModuleExecutor`, `IEnvironmentCatalog`, `IArtifactPublisher`, or `ITransactionManager` that workers must inject for normal execution.

Low-level runtime mechanisms may later be represented as internal DI services where lifetime or testability benefits from it, but ordinary modules should continue to receive one `IModuleRuntime` execution facade. Explicit transactional participation for custom DI services is a separate opt-in extension point described in [Transactional Services](transactional-services.md); it is not the basic runtime API.

## Transaction Migration Consequences

This responsibility split prepares the next implementation stages without predetermining concrete transaction types:

- per-invocation DI scope creation can wrap worker activation/dispatch without touching module-context semantics;
- the runtime environment scope can be replaced by a transaction-bound environment graph view;
- the named environment catalog becomes component state instead of root-runtime-owned mutable state;
- artifact publication can become staged environment changes;
- `ModuleContext` orchestration can establish the main invocation transaction and reconcile configuration before main activation;
- `IModuleRuntime` remains a narrow facade even as its backing state becomes execution-session-local.

The refactor should preserve current sequential behavior. It is preparation for the transaction model, not an opportunity to introduce transaction semantics early.
