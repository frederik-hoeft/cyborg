---
applyTo: "Source/Cyborg.Core.Aot/**/*.cs"
---

# Source Generator Instructions

When constructing generated C# source:

- For consecutive emitted lines whose indentation and structure are known together, prefer `IndentedStringBuilder.AppendBlock(...)` with interpolated raw string literals so the generated source remains visible as one coherent block.
- Avoid creating multiple `IndentedStringBuilder` views solely to step through fixed indentation one line at a time with `AppendLine(...)` calls. This tends to obscure the shape of the generated source.
- Use `IncreaseIndent(...)` when generation genuinely needs a nested insertion point for dynamic or recursive content. In mixed cases, prefer emitting the fixed prefix with `AppendBlock(...)`, rendering only the dynamic middle through the appropriately indented builder, and then emitting the fixed suffix with `AppendBlock(...)`.
- Do not force `AppendBlock(...)` when lines are independently conditional or assembled, or when a raw block would make the generator logic less clear. Optimize for seeing both the generator logic and the resulting source structure at a glance.
