# Engineering Backlog

This document tracks deferred, cross-cutting engineering improvements. These are review tasks rather than steady-state architecture contracts; implementation and architectural decisions belong in the relevant design documentation once the work is undertaken.

## Review interface-to-implementation casts for internal API access

**Status:** Backlog. **Scope:** Solution-wide (`Source/`), including runtime, source-generated integration, and supporting infrastructure.

Audit places where code accepts or depends on an interface but casts the value back to a concrete implementation to access capabilities absent from that interface. Such casts are not inherently incorrect, particularly when behavior genuinely depends on a specific implementation. However, using them as the normal mechanism for accessing internal APIs undermines the interface boundary, couples consumers to implementation types, and can silently introduce assumptions about which implementations are supported.

Current examples worth investigating include `ModuleValidationContext` requiring `RuntimeEnvironment` from `IRuntimeEnvironment`, `ModuleContextPreparer` accessing structural operations through `EnvironmentLike`, and environment publication and transactional import selecting concrete-type paths to preserve stored bindings. These are starting points, not an exhaustive inventory or a presumption that each cast must be removed.

Review the available approaches to exposing internal capabilities consistently, including internal members on interfaces where language and assembly boundaries permit, narrowly scoped internal capability interfaces, and a documented public but explicitly unstable/internal-by-convention facade where cross-assembly consumers or generated code require access. Consider whether some operations should instead be owned by a separate internal service. Account for source-generator boundaries, native-AOT constraints, visibility, external implementations, and compatibility before selecting a shared strategy.

**Acceptance:**

1. Inventory interface-to-implementation casts across the solution and classify genuine implementation-specific dispatch separately from casts used to recover missing interface capabilities.
2. Establish a consistent internal API exposure strategy, including when concrete casts remain justified and what guarantees an interface actually makes about supported implementations.
3. Refactor relevant call sites toward that strategy without changing runtime, environment, transaction, or generated-module semantics; preserve tests for affected integration paths.
4. Document the resulting interface/internal-API convention in the appropriate repository guidance or architecture documentation. Avoid broad public API expansion merely to eliminate casts.
