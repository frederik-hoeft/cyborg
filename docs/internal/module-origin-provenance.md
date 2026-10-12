# Definition-Site Provenance for Module Configuration

This note is the design for contextual origin variables on the `@` self-reference. The steady-state contract lives in [Interpolation and Override Resolution](../architecture/interpolation.md), [System Architecture](../architecture/architecture-overview.md), and [Module Reference](../architecture/modules-reference.md).

## Problem

A module configuration file often needs another workflow file that sits next to it. Ordinary paths are resolved against the process working directory, so `./child.jconf` means something different depending on who launched Cyborg. The module needs the directory of the file that **defined** it.

That location is not a workflow variable. It is a fact about the module definition. Storing it in the transactional variable map would publish it on join, let a parent-scoped child overwrite the caller, and let siblings observe it. Call-site provenance has the same problem: a template loaded from another file would report the caller's path instead of its own.

## Policy

`@` remains the current scope's namespace, and `@@` remains the entry-point namespace. Two contextual members extend that self-reference:

| Expression | Meaning |
|------------|---------|
| `@.origin.file` | Absolute path of the file that defined the module whose view is resolving `@` |
| `@.origin.directory` | Absolute directory containing that file |
| `@@.origin.file` | The same file path, read from the resolution entry point |
| `@@.origin.directory` | The same directory, read from the resolution entry point |

The paths are the canonical paths of the configuration load (full path, symlink-resolved). `Directory` does not have a trailing separator except when the file is at the filesystem root.

Provenance belongs to the definition, not the call.

- Every `ModuleReference` deserialized while a file is loading receives that file's origin, including nested and dynamic references in the same document.
- A reference loaded from another file keeps that file's origin when a caller executes it.
- `new ModuleReference(definition, moduleId)` has no origin. `new ModuleReference(definition, moduleId, origin)` sets one.
- With no origin, `@.origin.file` and `@.origin.directory` are undefined. They do not fall back to a parent module, a caller, or a stored variable.

The same names work through the existing value grammar: `${...}` interpolates them as text, `&{...}` reads them lazily, and `*{...}` snapshots them when the variable is defined. `*{@identifier}` stays reserved. `*{@.origin.file}` and `*{@@.origin.file}` are allowed because they name contextual members, not the entry-point modifier on an ordinary variable. At a `SetVariable` boundary the defining view is the entry point, so those two captures snapshot the same path.

Contextual names are runtime-owned and read-only. The whole `@.<identifier>` namespace is reserved, not only the two origin paths, so a later contextual member cannot be shadowed by a workflow write. `@identifier` override keys are unchanged: an override has no dot immediately after `@`.

## Mechanism

Origin is view metadata, like namespace and override-resolution tags. It is not a variable-store entry and it is not a field on the transactional environment node.

```text
ModuleOrigin
  -> IEnvironmentContext contributor
    -> environment view
      -> resolution of `@.` / `@@.` members
```

`IEnvironmentContext` answers a path such as `origin.file`. `OriginEnvironmentContext` is the first contributor. `EnvironmentContext.Compose` layers contributors; the first that defines a path wins. `ModuleExecutionContext.Create` is the only bind-site seam. A future fact adds a contributor there and does not change parsing or reconciliation.

File loads push the canonical path on an async-local scope around deserialization. `ModuleReferenceJsonConverter` stamps `ModuleOriginScope.Current` onto each reference it builds. Loads nest, and the scope restores the previous origin when it exits. Deserialization outside a load, including in-memory JSON, stamps nothing.

Execution installs context in one place: `ExecuteModuleReferenceInCurrentScopeAsync`. The reference's origin replaces whatever context the caller view carried, including when the scope is `Parent` or `Current`. A missing origin installs an empty context. Later `with` copies and transaction rebinds preserve the installed context. They do not write it into the fork.

Resolution treats a name shaped as `@.` plus a valid identifier as contextual:

- the view that is resolving answers it;
- an undefined contextual name stops the walk;
- stored variables and parent environments are not consulted.

`@@.<member>` is parsed as that same contextual name with entry-point origin, matching `@` / `@@` for the namespace. Inherited lookup of an ordinary value still evaluates the value on the environment that stores it, so `@` inside that value is the storing view and `@@` is the view that started the read.

Reconstruction of an environment from the transaction log does not invent context. When the inheritance link points at a view that is still live on the runtime context, the link reuses that view (rebound into the current transaction) so the parent's definition-site context remains visible for values stored on the parent. The reused view is not copied into the node. A view with no live counterpart has no contextual values; eager capture is how a path is published into workflow state that must outlive the execution view.

## Transactional isolation

Join, rollback, and sibling reconciliation publish variable-store changes and named-environment topology. They do not publish contextual state, because that state was never in the participant. Consequences:

- a child that uses `Parent` or `Current` cannot leave its origin on the caller;
- a sibling cannot read another branch's origin through the reconciled store;
- deleting or assigning `@.origin.file` fails before the store is touched;
- enumeration and artifact copy see stored variables only.

Workflow code that needs the path as data captures or interpolates it into an ordinary variable. That ordinary variable then follows normal transaction rules. The contextual name itself never does.

## Composition

Inner and outer origins do not override each other. A sequence defined in `jobs/daily.jconf` that loads `templates/backup.jconf` sees `jobs` for its own `@.origin.directory` and the template sees `templates` for its own. A dynamic module executes the referenced definition's origin, not the dynamic module's file, when those files differ. An indirect `ModuleReference` keeps the origin it was given at construction or load; the reference object is the carrier, not the environment it was stored in.

## Compatibility

Existing `@`, `@@`, `@identifier`, and override keys parse and resolve as before. `@.member` and `@@.member` were not valid expressions. References constructed without an origin, and every module that does not read these names, behave as they do today.
