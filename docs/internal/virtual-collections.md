# Virtual Collections

This note is the design for dynamically assembled environment collections. The steady-state description lives in [Cyborg Architecture](../architecture/architecture-overview.md#virtual-collections), [Transactional Execution](../architecture/transactions.md#environment-graph-and-bindings), and [Module Reference](../architecture/modules-reference.md#foreach-cyborgmodulesforeachv1).

## Problem

Environment collections are CLR objects stored under an ordinary variable name. A consumer can iterate one only after some earlier step has materialized the whole collection. Workflows that discover elements over time have to rebuild and assign that object, and parallel branches that each produce elements collide on the single binding.

Collections still have to participate in the existing environment model: variable syntax, scoped resolution, transactional commit and rollback, and the validation pipeline that runs when a module binds a value. `IEnvironmentLike` stays the public read/write surface. Callers that already resolve a CLR collection must not learn a second API, and they must not start observing virtual collections by accident.

## Collection names

A virtual collection is addressed by suffixes on a normal variable path. The identifier grammar is unchanged. The suffixes are collection operations rather than part of an identifier or namespace. They also compose with module-property override addresses, so `@module.items[]+` appends to a virtual collection that can supply the `items` override.

| Name | Write | Read |
|------|-------|------|
| `myCollection[]+` | Append the assigned value as one element. Creates the collection when it does not exist. | Not readable. |
| `myCollection[]` | Define or replace the collection. | Stable snapshot taken when the name is resolved. |
| `myCollection[+]` | Not assignable. | Live enumeration of the same collection. |

`myCollection` remains an ordinary variable. A CLR collection stored there is untouched, so existing workflows keep working. The virtual collection of the same path is a different binding. Ordinary override values follow the same rule: `@module.items` remains the existing whole-value override, while `@module.items[]` addresses a virtual collection at that override path.

Defining `myCollection[]` makes the collection present even when the definition has no elements. `null`, or any empty non-string sequence, is an empty definition. A successful read of an empty collection returns an empty sequence. A read of a name that was never defined and has no elements fails, which is the same result as an undefined variable. Removing `myCollection[]` or `myCollection[+]` removes that collection from the current environment.

Append stores the assigned object as the element. It does not enumerate it and it does not decompose it. Assigning a sequence to `myCollection[]` is the definition form: the sequence is copied as the new element list. `string` is a single element in both forms, matching the rule that text is not a collection of characters. A definition is materialized before any existing elements are replaced, so the source can be a snapshot of the same collection.

## Reads

Both readable names resolve through `TryResolveVariable`. Snapshot resolution returns the elements copied at that call. Live resolution returns an enumeration that reads the collection again as it moves forward.

The live enumeration is not a subscription. It consumes the currently visible, not-yet-yielded elements as one ordered batch, then refreshes the environment before deciding whether another batch exists. Elements that become visible during enumeration are therefore visited after the current batch, while identities that were already yielded are never replayed even if reconciliation later exposes an element with an earlier internal identity. When a refresh contains no new elements, enumeration stops and does not wait for a later append. A new resolution sees the collection as it is at that later call.

Elements are returned as they were stored. The collection read does not interpolate them, cast them, or rebuild them from decomposed leaves. Recomposition is out of scope. A typed consumer casts at binding time. `ResolveCollection` materializes a virtual collection into the property's element type while the module is prepared, so a mismatch fails in the existing validation pipeline. `Foreach` resolves `IEnumerable<object>` and binds each element into the iteration environment; the iteration body then validates whatever it reads. Append does not check element types, and a virtual collection does not require one CLR element type.

Exact indirection (`${myCollection[]}` or `${myCollection[+]}` as the entire stored string) preserves the collection object. The same expressions inside a larger string follow ordinary interpolation and use the resolved value's text form.

## Storage and visibility

Element identity and the empty-definition marker are stored beside ordinary variables, under keys that are not valid variable references. Enumerating an environment does not yield those keys. A present virtual collection is visible as one `name[]` entry whose value is a snapshot of the elements in that environment. Copying an environment through enumeration and `SetVariable`, including artifact publication, replays that entry as a definition. Adoption of an environment into a transaction uses the same visible entries, so the new transaction receives definitions rather than storage keys.

Inheritance follows ordinary shadowing. A read uses the nearest environment that has the collection. An append or definition in a child environment creates or replaces the child's collection and hides the parent collection of the same name. It does not extend the parent. Branches that should add to one collection select the same logical environment (`current`, `parent`, `global`, or one named environment).

Publishing a decomposable value whose root is already a collection assignment stores that value as one element and does not walk its leaves. `LeavesOnly`, `Shallow`, and `FullHierarchy` all mean the same thing on that root. Publishing any other root is unchanged, including when a leaf happens to be a CLR collection.

## Transactions

Each append allocates a process-local monotonic element identity and stores the element as a separate hidden environment binding. Sequential appends therefore retain their allocation order, while concurrently executing branches may interleave according to scheduling. No total semantic order is assigned to simultaneous sibling writes. The identity exists only to keep element bindings distinct and sortable; it is not transaction state.

Because different appends change different environment bindings, the existing environment transaction participant combines parallel additions without collection-specific logic in the generic transaction engine. Siblings still observe the same fork baseline and cannot see one another's additions until reconciliation. Rollback withholds a branch's element bindings together with its other workflow-data writes.

Explicit collection definition uses one hidden presence marker. Empty definitions use a merge-compatible marker, so branches that independently establish the same empty collection can reconcile when their other changes are compatible. Competing non-empty definitions remain replacement writes and conflict rather than being combined accidentally. A definition in one branch and independent appends in another can reconcile because the marker and appended elements occupy different bindings.

The environment participant owns these collection-specific merge semantics. `ModuleTransaction` and the generic transaction coordinator treat virtual-collection element and marker keys as opaque participant state and contain no collection-ordering policy.

## Compatibility

`SetVariable`, `TryResolveVariable`, `TryRemoveVariable`, enumeration, and `Publish` keep their signatures. Public variable operations validate ordinary paths, override addresses, the artifact exit-status leaf, and collection operators instead of accepting arbitrary storage keys. CLR collections assigned to ordinary names still resolve as those objects. Virtual collections are opt-in by name. Module workers that already iterate `IEnumerable<object>` or bind `IReadOnlyCollection<T>` do not branch on how the collection was assembled.

Generated collection-property overrides first honor an existing ordinary override such as `@module.items`. If none exists, the same address may be assembled as a virtual collection through `@module.items[]` and `@module.items[]+`; binding materializes a snapshot into the declared element type. Direct `@module.items[+]` resolution remains a live environment view, but prepared `IReadOnlyCollection<T>` module properties are snapshots at binding time. In interpolation syntax, `${@items[]}` keeps the pre-existing meaning of an entry-point reference to `items[]`; the `@` inside `${...}` is not an override-address prefix.
