# Validation Attributes Reference

This document provides a complete reference for the validation, defaulting, and override control attributes used by the Cyborg source generators. These attributes are declared in `Cyborg.Core.Aot` and emitted into consuming compilations. They are applied to properties on module records (marked with `[GeneratedModuleValidation]`) and on nested records (marked with `[Validatable]`).

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
  - [Required](#required)
  - [Range](#range)
  - [MinLength](#minlength)
  - [MaxLength](#maxlength)
  - [ExactLength](#exactlength)
  - [Length](#length)
  - [MatchesRegex](#matchesregex)
  - [MatchesGrammar](#matchesgrammar)
  - [FileExists](#fileexists)
  - [DirectoryExists](#directoryexists)
  - [DefinedEnumValue](#definedenumvalue)
- [Default Value Attributes](#default-value-attributes)
  - [DefaultValue](#defaultvalue)
  - [DefaultInstance](#defaultinstance)
  - [DefaultInstanceFactory](#defaultinstancefactory)
  - [DefaultTimeSpan](#defaulttimespan)
- [Override Control Attributes](#override-control-attributes)
  - [IgnoreOverrides](#ignoreoverrides)
- [Decomposition Attributes](#decomposition-attributes)
  - [DecomposeIgnore](#decomposeignore)

<!-- /code_chunk_output -->


## Generator Trigger Attributes

These attributes trigger source generation on the annotated type. They are not applied to individual properties.

### GeneratedModuleValidation

Triggers the validation generator on a module record. The target must be a `partial record`. The generator emits implementations of `ApplyDefaultsAsync`, `ResolveOverridesAsync`, and `ValidateAsync` based on the attributes applied to the record's properties.

**Target:** `class` (record)

### Validatable

Marks a nested record type for recursive validation. When a property on a `[GeneratedModuleValidation]` record has a type marked `[Validatable]`, the generated pipeline applies defaults, overrides, and validation recursively to that nested record's properties.

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

These attributes declare constraints that are checked during the `ValidateAsync` stage of the generated pipeline. If a constraint is violated, a `ValidationError` is added to the result. All validation attributes target properties.

### Required

Validates that the property has a meaningful value. For strings, checks that the value is not null or whitespace. For other types, checks that the value is not equal to its type's default (e.g., `0` for integers, `null` for reference types).

### Range

Validates that a comparable property value falls within specified bounds. Either or both bounds may be specified.

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

### DefinedEnumValue

Validates that an enum property contains a defined value (i.e., not a raw integer cast to the enum type). Uses `Enum.IsDefined` semantics.


## Default Value Attributes

These attributes declare default values applied during the `ApplyDefaultsAsync` stage. Defaults are applied when a property has a null value (for reference types) or a type-default value (for value types). Default application occurs recursively on nested `[Validatable]` records and collection elements.

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

Provides a default `TimeSpan` value parsed from a string at compile time. The string must be in a format accepted by `TimeSpan.Parse` with invariant culture.

**Parameters:**

- `TimeSpan` — String representation of the default duration (e.g., `"00:30:00"` for 30 minutes).


## Override Control Attributes

### IgnoreOverrides

Prevents the override resolution system from applying environment-driven overrides to the annotated property. The property retains its deserialized or defaulted value regardless of any matching override keys in the environment.

This is typically applied to structural properties (such as `Name`, `Group`, or `Artifacts`) that should not be overridden at runtime, or to properties whose types are not compatible with the string-based override resolution mechanism.


## Decomposition Attributes

### DecomposeIgnore

Excludes a property from the generated `IDecomposable.Decompose()` output. The property is not projected into a `DynamicKeyValuePair` entry and is not addressable via hierarchical variable paths in the environment.
