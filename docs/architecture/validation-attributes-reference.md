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
- [Decomposition Attributes](#decomposition-attributes)
  - [DecomposeIgnore](#decomposeignore)

<!-- /code_chunk_output -->


## Generator Trigger Attributes

These attributes trigger source generation on the annotated type. They are not applied to individual properties.

### GeneratedModuleValidation

Triggers the validation generator on a module record. The target must be a `partial record`. The generator emits private `ApplyDefaultsAsync`, `ResolveOverridesAsync`, and `ApplyInterpolationAsync` preparation helpers together with the public `ValidateAsync` orchestrator, based on the attributes applied to the record's properties.

**Target:** `class` (record)

### Validatable

Marks a nested record type for recursive validation. When a property on a `[GeneratedModuleValidation]` record has a type marked `[Validatable]`, the generated pipeline applies defaults, overrides, interpolation, and validation recursively to that nested record's properties.

**Target:** `class` (record)

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

These attributes declare constraints that are checked during the `ValidateAsync` stage of the generated pipeline. If a constraint is violated, a `ValidationError` is added to the result. All validation attributes are applied to properties; selected attributes can redirect their constraint to each immediate element of a collection property.

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

In this example, the first attribute rejects a null collection while the second rejects null or whitespace elements. Attribute-specific type requirements are evaluated against the element type when `TargetsElements` is enabled; for example, `[VariableIdentifier(TargetsElements = true)]` requires a collection of strings. Applying `TargetsElements` to a non-collection property, or to an incompatible element type, produces a source-generator diagnostic. Targeting is limited to the immediate elements of the annotated collection; it does not recursively apply the same attribute to deeper collection layers.

### Required

Validates that the property has a meaningful value. For strings, checks that the value is not null or whitespace. For other types, checks that the value is not equal to its type's default (e.g., `0` for integers, `null` for reference types, or a default `ImmutableArray<T>`). An initialized empty collection is distinct from a default immutable array and requires a length constraint when emptiness itself is invalid.

### Range

Validates that a comparable property value falls within specified bounds. At least one bound must be specified; either bound may be omitted.

**Parameters:**

- `Min` (optional) — Minimum allowed value, inclusive.
- `Max` (optional) — Maximum allowed value, inclusive.

The attribute is generic: `[Range<int>]`, `[Range<long>]`, `[Range<double>]`, etc. The type parameter must match the property type and implement `IComparable<T>`.

### MinLength

Validates that a string or collection property has at least the specified number of elements or characters.

**Parameters:**

- `Min` — Minimum length, inclusive.

### MaxLength

Validates that a string or collection property has at most the specified number of elements or characters.

**Parameters:**

- `Max` — Maximum length, inclusive.

### ExactLength

Validates that a string or collection property has exactly the specified number of elements or characters.

**Parameters:**

- `Length` — Required length.

### Length

Validates that a string or collection property length falls within a range. Combines the behavior of `[MinLength]` and `[MaxLength]` into a single attribute.

**Parameters:**

- `Min` — Minimum length, inclusive.
- `Max` — Maximum length, inclusive.

### VariableIdentifier

Validates that a string conforms to the canonical environment variable-identifier grammar used by `IRuntimeEnvironment.SyntaxFactory.IsValidIdentifier`. An identifier starts with an ASCII letter, underscore, or hyphen. Subsequent characters may be ASCII letters, digits, underscores, or hyphens; periods may separate non-empty suffixes. Empty segments, consecutive periods, and trailing periods are invalid. See [Architecture Overview -- Variable Name Syntax](architecture-overview.md#variable-name-syntax) for the complete variable and interpolation grammar.

Null values are ignored by this constraint; combine it with `[Required]` when null must also be rejected. Because validation follows interpolation, the final interpolated value is checked rather than the original placeholder expression.

**Applies to:** `string` properties, or immediate `string` collection elements when `TargetsElements = true`.

### MatchesRegex

Validates that a string property matches a regular expression. The regex is referenced by member name — the attribute points to a static property or field on the containing type that provides the `Regex` instance.

**Parameters:**

- `RegexMemberName` — Name of a static member on the module type returning a `Regex` instance. The member should use `[GeneratedRegex]` for AOT compatibility.

### MatchesGrammar

Validates that a string property can be parsed by a grammar. The parser is referenced by member name — the attribute points to a static property or field on the containing type that provides an `IParser` instance.

**Parameters:**

- `ParserMemberName` — Name of a static member on the module type returning an `IParser` instance.

### FileExists

Validates that the string property contains a path to an existing file. Checked at validation time against the file system.

### DirectoryExists

Validates that the string property contains a path to an existing directory. Checked at validation time against the file system.

### FileName

Validates that a string contains a valid file name: it must be non-empty, must not be `.` or `..`, and must not contain characters returned by `Path.GetInvalidFileNameChars()`.

**Applies to:** `string` properties only.

### RootedPath

Validates that a string contains a rooted (absolute) path according to `Path.IsPathRooted`.

**Applies to:** `string` properties only.

### UnrootedPath

Validates that a string contains an unrooted (relative) path according to `Path.IsPathRooted`.

**Applies to:** `string` properties only.

### NormalizedPath

Validates that a string contains a normalized path: no segment may be `.` or `..`, and consecutive directory separators may not create empty segments. The check does not resolve the path against the current working directory.

**Applies to:** `string` properties only.

### DefinedEnumValue

Validates that an enum property contains a defined value (i.e., not a raw integer cast to the enum type). Uses `Enum.IsDefined` semantics.


## Default Value Attributes

These attributes declare default values applied during the `ApplyDefaultsAsync` stage. Defaults are applied when a property has a null value (for reference types) or a type-default value (for value types). Default application occurs recursively on nested `[Validatable]` records and supported collection elements. Null collections, absent nullable value-type collections, and default immutable arrays are not enumerated; a default `ImmutableArray<T>` is preserved unless an explicit property default replaces it.

### DefaultValue

Provides a compile-time constant default value for a property. The attribute is generic and the type parameter must match the property type.

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

Prevents the generated interpolation phase from calling `runtime.Environment.Interpolate(...)` for the annotated string property. The raw string is preserved so a worker can interpolate it later, after context-specific variables or child artifacts exist.

**Applies to:** `string` properties only.

This is used by `AssertModule.Message`, whose placeholders may refer to artifacts produced by the assertion module and therefore cannot be resolved during pre-execution validation. `ModuleBase.Name` and `ModuleBase.Group` also opt out because they define the environment namespace before interpolation runs.


## Decomposition Attributes

### DecomposeIgnore

Excludes a property from the generated `IDecomposable.Decompose()` output. The property is not projected into a `DynamicKeyValuePair` entry and is not addressable via hierarchical variable paths in the environment.
