# Validation Attributes Reference

This document provides a complete reference for the validation, defaulting, override-control, and interpolation-control attributes used by the Cyborg source generators. These attributes are declared in `Cyborg.Core.Aot` and emitted into consuming compilations. They are applied to properties on module records (marked with `[GeneratedModuleValidation]`) and on nested records (marked with `[Validatable]`).

For how these attributes are processed by the source generators, see [Source Generators](source-generators.md). For the runtime systems that consume the generated validation pipeline, see [Architecture Overview](architecture-overview.md#validation-pipeline).

**Table of Contents**

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Generator Trigger Attributes](#generator-trigger-attributes)
  - [GeneratedModuleValidation](#generatedmodulevalidation)
  - [Validatable](#validatable)
  - [GeneratedModuleLoaderFactory](#generatedmoduleloaderfactory)
  - [GeneratedDecomposition](#generateddecomposition)
- [Validation Attributes](#validation-attributes)
  - [Collection Element Targeting](#collection-element-targeting)
  - [Required](#required)
  - [Range](#range)
  - [MinLength](#minlength)
  - [MaxLength](#maxlength)
  - [ExactLength](#exactlength)
  - [Length](#length)
  - [VariableIdentifier](#variableidentifier)
  - [MatchesRegex](#matchesregex)
  - [MatchesGrammar](#matchesgrammar)
  - [FileExists](#fileexists)
  - [DirectoryExists](#directoryexists)
  - [FileName](#filename)
  - [RootedPath](#rootedpath)
  - [UnrootedPath](#unrootedpath)
  - [NormalizedPath](#normalizedpath)
  - [DefinedEnumValue](#definedenumvalue)
- [Default Value Attributes](#default-value-attributes)
  - [DefaultValue](#defaultvalue)
  - [DefaultInstance](#defaultinstance)
  - [DefaultInstanceFactory](#defaultinstancefactory)
  - [DefaultTimeSpan](#defaulttimespan)
- [Override and Interpolation Control Attributes](#override-and-interpolation-control-attributes)
  - [IgnoreOverride](#ignoreoverride)
  - [IgnoreInterpolation](#ignoreinterpolation)
  - [Secret](#secret)
  - [Untagged](#untagged)
- [Decomposition Attributes](#decomposition-attributes)
  - [DecomposeIgnore](#decomposeignore)

<!-- /code_chunk_output -->


## Generator Trigger Attributes

These attributes trigger source generation on the annotated type. They are not applied to individual properties.

### GeneratedModuleValidation

Triggers the validation generator on a module record. The target must be a `partial record`. The generator emits private `ApplyDefaultsAsync`, `ResolveOverridesAsync`, and `ApplyInterpolationAsync` preparation helpers together with the public `ValidateAsync` orchestrator, based on the attributes applied to the record's properties.

**Target:** `class` (record)

### Validatable

Marks a nested record type for recursive validation. Record classes and record structs are supported. When a property on a `[GeneratedModuleValidation]` record has a type marked `[Validatable]`, the generated pipeline applies defaults, overrides, interpolation, and validation recursively to that nested record's properties. Nullable record values are treated as absent until a value exists; non-nullable record structs are traversed directly.

**Target:** `class` or `struct` (record)

### GeneratedModuleLoaderFactory

Triggers the loader factory generator on a module loader class. The target must be a `partial class` inheriting from `ModuleLoader<TWorker, TModule>`. The generator emits a factory method that constructs the worker by resolving constructor dependencies from the DI container.

**Target:** `class`  
**Parameters:**

- `Name` (optional) — Custom name for the generated method. Defaults to `CreateWorker`.

### GeneratedDecomposition

Triggers the decomposition generator on a record or class. The generator emits an `IDecomposable.Decompose()` method that projects public properties into `DynamicKeyValuePair` entries.

**Target:** `class` (record or class)  
**Parameters:**

- `NamingPolicyProvider` (optional) — The type containing the naming policy. Defaults to `JsonNamingPolicy`.
- `NamingPolicy` (optional) — The static property name on the provider type. Defaults to `"SnakeCaseLower"`.


## Validation Attributes

These attributes declare constraints that are checked during the `ValidateAsync` stage of the generated pipeline. If a constraint is violated, a `ValidationError` is added to the result. Generated errors carry a user-facing recursive property path (for example, `Options.Path`, `Tags[2]`, or `Items[1].Value`) so nested and collection failures remain attributable to the concrete input location. All validation attributes are applied to properties; selected attributes can redirect their constraint to each immediate element of a collection property.

For validation purposes, a **textual** or **string-like** target means either `string` or `TaggedString`. String-oriented constraints inspect `TaggedString.Value`, while any value included in a generated diagnostic is rendered through the validation context so secret-tagged text is not exposed by the error message.

### Collection Element Targeting

`Required`, `MinLength`, `MaxLength`, `ExactLength`, `Length`, and `VariableIdentifier` derive from `PropertyValidationAttribute` and expose the following named property:

- `TargetsElements` (optional, default `false`) — When `false`, validates the annotated property. When `true`, validates each immediate element of a supported collection property instead.

Element-targeted validation runs after defaults, overrides, and interpolation, so constraints observe the same final values as ordinary property validation. The containing collection is not constrained by an element-targeted attribute. Null reference collections, absent nullable value-type collections, and default `ImmutableArray<T>` values are not enumerated.

The supporting attributes allow multiple applications, so a collection and its elements can be constrained independently:

```csharp
[Required]
[Required(TargetsElements = true)]
IReadOnlyCollection<string?>? Values
```

In this example, the first attribute rejects a null collection while the second rejects null or whitespace elements. Attribute-specific type requirements are evaluated against the element type when `TargetsElements` is enabled; for example, `[VariableIdentifier(TargetsElements = true)]` requires a collection of `string` or `TaggedString` values. Applying `TargetsElements` to a non-collection property, or to an incompatible element type, produces a source-generator diagnostic. Targeting is limited to the immediate elements of the annotated collection; it does not recursively apply the same attribute to deeper collection layers.

### Required

Validates that the property has a meaningful value. For `string` and `TaggedString`, checks that the textual value is not null or whitespace. Collection presence follows the same collection-shape semantics used by traversal: null references, absent nullable value types, and default `ImmutableArray<T>` values are missing. Other values are compared with their type default (for example, `0` for integers). An initialized empty collection is distinct from a default immutable array and requires a length constraint when emptiness itself is invalid.

### Range

Validates that a comparable property value falls within specified bounds. At least one bound must be specified; either bound may be omitted.

**Parameters:**

- `Min` (optional) — Minimum allowed value, inclusive.
- `Max` (optional) — Maximum allowed value, inclusive.

The attribute is generic: `[Range<int>]`, `[Range<long>]`, `[Range<double>]`, etc. The type parameter must match the property type and implement `IComparable<T>`.

### MinLength

Validates that a textual or countable collection property has at least the specified number of elements or characters.

**Parameters:**

- `Min` — Minimum length, inclusive.

### MaxLength

Validates that a textual or countable collection property has at most the specified number of elements or characters.

**Parameters:**

- `Max` — Maximum length, inclusive.

### ExactLength

Validates that a textual or countable collection property has exactly the specified number of elements or characters.

**Parameters:**

- `Length` — Required length.

### Length

Validates that a textual or supported countable collection property length falls within a range. Combines the behavior of `[MinLength]` and `[MaxLength]` into a single attribute. Arrays and `ImmutableArray<T>` use their native `Length`; other countable collection shapes use their `IReadOnlyCollection<T>` count contract. Collection absence is guarded before count access, so null collections and default `ImmutableArray<T>` values are skipped rather than treated as empty or dereferenced.

**Parameters:**

- `Min` — Minimum length, inclusive.
- `Max` — Maximum length, inclusive.

### VariableIdentifier

Validates that a textual value conforms to the canonical environment variable-identifier grammar used by `IRuntimeEnvironment.SyntaxFactory.IsValidIdentifier`. An identifier starts with an ASCII letter, underscore, or hyphen. Subsequent characters may be ASCII letters, digits, underscores, or hyphens; periods may separate non-empty suffixes. Empty segments, consecutive periods, and trailing periods are invalid. See [Architecture Overview -- Variable Name Syntax](architecture-overview.md#variable-name-syntax) for the complete variable and interpolation grammar.

Null values are ignored by this constraint; combine it with `[Required]` when null must also be rejected. Because validation follows interpolation, the final interpolated value is checked rather than the original placeholder expression.

**Applies to:** `string` or `TaggedString` properties, or immediate collection elements of either type when `TargetsElements = true`.

### MatchesRegex

Validates that a `string` or `TaggedString` property matches a regular expression. The regex is referenced by member name — the attribute points to a static property or field on the containing type that provides the `Regex` instance.

**Parameters:**

- `RegexMemberName` — Name of a static member on the module type returning a `Regex` instance. The member should use `[GeneratedRegex]` for AOT compatibility.

### MatchesGrammar

Validates that a `string` or `TaggedString` property can be parsed by a grammar. The parser is referenced by member name — the attribute points to a static property or field on the containing type that provides an `IParser` instance.

**Parameters:**

- `ParserMemberName` — Name of a static member on the module type returning an `IParser` instance.

### FileExists

Validates that a `string` or `TaggedString` property contains a path to an existing file. Checked at validation time against the file system.

### DirectoryExists

Validates that a `string` or `TaggedString` property contains a path to an existing directory. Checked at validation time against the file system.

### FileName

Validates that a textual value contains a valid file name: it must be non-empty, must not be `.` or `..`, and must not contain characters returned by `Path.GetInvalidFileNameChars()`.

**Applies to:** `string` and `TaggedString` properties.

### RootedPath

Validates that a textual value contains a rooted (absolute) path according to `Path.IsPathRooted`.

**Applies to:** `string` and `TaggedString` properties.

### UnrootedPath

Validates that a textual value contains an unrooted (relative) path according to `Path.IsPathRooted`.

**Applies to:** `string` and `TaggedString` properties.

### NormalizedPath

Validates that a textual value contains a normalized path: no segment may be `.` or `..`, and consecutive directory separators may not create empty segments. The check does not resolve the path against the current working directory.

**Applies to:** `string` and `TaggedString` properties.

### DefinedEnumValue

Validates that an enum property contains a defined value (i.e., not a raw integer cast to the enum type). Uses `Enum.IsDefined` semantics.


## Default Value Attributes

These attributes declare default values applied during the `ApplyDefaultsAsync` stage. Defaults are applied when a property has a null value (for reference types) or a type-default value (for value types). Default application occurs recursively on nested `[Validatable]` records and supported collection elements. Null collections, absent nullable value-type collections, and default immutable arrays are not enumerated; a default `ImmutableArray<T>` is preserved unless an explicit property default replaces it.

### DefaultValue

Provides a compile-time constant default value for a property. The attribute is generic and the type parameter normally matches the property type. `TaggedString` properties use `DefaultValue<string>` because attribute arguments must be compile-time constants; the generated assignment converts the literal string to an untagged `TaggedString` before later preparation invariants such as `[Secret]` are applied.

**Parameters:**

- `Value` — The default value to apply.
- `WhenPresent` (optional, params) — Additional values that, when present, also trigger default substitution. This allows treating specific sentinel values as equivalent to "not set".

Example: `[DefaultValue<int>(22)]` applies a default of 22 when the property is 0. `[DefaultValue<int>(22, -1)]` also treats -1 as "not set".

### DefaultInstance

Provides a default by calling the static `Default` property on the property's type. The type must implement `IDefaultInstance<T>`, which exposes a `static T Default { get; }` member.

### DefaultInstanceFactory

Provides a default by calling a named static factory method on the containing module type. The method must return a value assignable to the property type.

**Parameters:**

- `FactoryMethod` — Name of a static method on the module type that returns the default value.

### DefaultTimeSpan

Provides a default `TimeSpan` value parsed from a string at compile time. The string must use the invariant constant (`c`) format accepted by `TimeSpan.ParseExact`.

**Parameters:**

- `TimeSpan` — String representation of the default duration (e.g., `"00:30:00"` for 30 minutes).


## Override and Interpolation Control Attributes

### IgnoreOverride

Prevents environment-driven override resolution for the annotated property.

**Parameters:**

- `recurse` (optional constructor argument, default `false`) — When `false`, only the annotated property itself ignores override resolution while eligible nested properties may still be processed. When `true`, the complete annotated subtree ignores overrides.

`ModuleBase.Name` and `ModuleBase.Group` use this attribute because environment binding consumes their structural identity before validation begins.

### IgnoreInterpolation

Prevents the generated interpolation phase from calling `runtime.Environment.Interpolate(...)` for the annotated string or `TaggedString` property. The value is preserved so a worker can interpolate it later, after context-specific variables or child artifacts exist.

**Applies to:** `string` and `TaggedString` properties.

This is used by `AssertModule.Message`, whose placeholders may refer to artifacts produced by the assertion module and therefore cannot be resolved during pre-execution validation. `ModuleBase.Name` and `ModuleBase.Group` also opt out because they define the environment namespace before interpolation runs.

### Secret

Valid only on `TaggedString` properties. Declares `cyborg.secret.v1` as an intrinsic property tag. Generated preparation ensures the tag is present both before and after override selection, so an override may replace the value but cannot declassify the property; final validation asserts the invariant. This is independent of interpolation, including when `[IgnoreInterpolation]` defers evaluation. Cyborg-controlled display surfaces render the resulting tagged value through `ITaggedStringRenderer`, which redacts the built-in secret tag as `[REDACTED]`.

**Applies to:** `TaggedString` properties only. Combining `[Secret]` with `[Untagged]` is an error.

### Untagged

Marks a string property as intentionally unable to carry tags. Suppresses `CYBORGVAL025`, which otherwise warns that interpolatable string properties should migrate to `TaggedString` so tags such as secrets propagate.

**Applies to:** `string` properties only.


## Decomposition Attributes

### DecomposeIgnore

Excludes a property from the generated `IDecomposable.Decompose()` output. The property is not projected into a `DynamicKeyValuePair` entry and is not addressable via hierarchical variable paths in the environment.
