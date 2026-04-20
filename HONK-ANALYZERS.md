# HONK Analyzer Catalogue

Fork-owned Roslyn rules that guard the fork's `[Access]` workaround
policy. Rules run on every `dotnet build` via the
`Content.Analyzers.Honk` project reference.

## Rules

| ID       | Severity | Summary                                                                |
| -------- | -------- | ---------------------------------------------------------------------- |
| HONK0001 | Error    | `[Access(..., Other = ReadWrite)]` forbidden inside a `// HONK` block. |
| HONK0004 | Error    | Unmatched `// HONK START` / `// HONK END` markers in one file.         |

## Suppressing a rule

If a rule fires on code that really needs to stay, suppress at the
call site with a comment explaining why:

```csharp
#pragma warning disable HONKxxxx // reason here; link an issue if relevant
component.Field = value;
#pragma warning restore HONKxxxx
```

Do not lower severity in `.editorconfig` to silence a rule
project-wide, that defeats the purpose of having the check.

## Adding a new rule

1. Implement the analyzer under `Content.Analyzers.Honk/`.
2. Add positive and negative tests under `Content.Analyzers.Honk.Tests/`.
3. Add a row to the table above.
