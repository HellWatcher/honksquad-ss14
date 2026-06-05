# Release Audit — Roslyn Analyzer (RA) Opportunities

**Scope:** the `release` branch tree (== current HEAD, `4b511716`).
**Goal:** find places to add new fork-owned Roslyn analyzers (`HONK00NN`).
**Method:** 5 parallel investigation lenses (ECS correctness, networking/prediction,
performance/allocation, localization/logging, lifecycle/fork-hygiene), each scoped to
**fork code** — files under any `/RussStation/` directory, files ending `.Honk.cs`, or
code inside `// HONK START` … `// HONK END` blocks. All headline numbers below were
re-verified with ripgrep against the fork file set (~357 `RussStation` + 8 `.Honk.cs`
files; 90 fork EntitySystems, 85 fork components).

---

## Executive summary

The single most important finding is a **meta-finding**: the fork is already
*unusually disciplined*. The existing 23 analyzers (`HONK0001`–`HONK0023`) plus good
coding habits have already closed most of the classic SS14 footguns:

- TryComp/Resolve discipline: 142/147 `TryComp` calls are properly guarded; **1** total
  `Comp<>()` and **1** `GetComponent<>()` in the whole fork; **0** ignored `Resolve`.
- Networking: `NetEntity` is used **40×** across events/BUI/DoAfter payloads; no
  hand-rolled component state; no parameterless `Dirty()`.
- Player strings: **0** hardcoded literals across 129 popup + 19 examine call sites.
- Performance: **0** uses of the slow `EntityQuery<T>()` IEnumerable overload; **0** LINQ
  inside per-tick loops.
- Lifecycle: all `[Dependency]` fields `readonly`; all `TryGetSolution`/`ResolveSolution`
  returns checked; DoAfter args set `BreakOnMove`/`NeedHand` everywhere.

**Consequence:** most new analyzers here would be *regression guards* (catch the first
future slip) rather than bug finders for code that exists today. That is still valuable,
but it changes the ROI ranking. The exceptions — where there are **real, current hits** —
are the **fork-drift "stringly-typed" family** and a couple of **per-tick perf** patterns.

### Top picks (ranked, build in this order)

| Rank | Proposed | Lens | Current hits | Why |
|---|---|---|---|---|
| 1 | `HONK0024` Untyped damage-type string literal → `ProtoId<DamageTypePrototype>` | hygiene | **29** | Real drift risk, real volume, extends the ProtoId family (HONK0014) |
| 2 | `HONK0025` Raw `EntityUid` in `[NetSerializable]`/event/BUI/DoAfter type | networking | ~0 | Canonical desync footgun; strong existing `NetEntity` convention to lock in; partial auto-fixer |
| 3 | `HONK0026` Duplicated `ProtoId<T>` constant across fork files | hygiene | ≥2 confirmed | Genuine maintenance hazard; compilation-wide; very low FP |
| 4 | `HONK0027` Eager `Loc.GetString`/popup inside an `Update` `MoveNext` loop | perf | confirmed (Payroll) | Cleanest hot-path anchor unique to SS14; real GC cost |
| 5 | `HONK0028` Interpolated `$""` in fork logging calls | hygiene/perf | **13** | Allocates before level check; near-zero FP; info severity |
| 6 | `HONK0029` Opaque dynamic `Loc.GetString` key (leading interpolation hole) | localization | 1–4 | Untranslatable, unlinTable keys ship silently |

Tier-3 (cheap regression guards, no current hits — build when convenient): generalize
`HONK0011` to any receiver (whitelist MetaData/Transform); bare-statement
`Resolve`/`TryComp` discard; `Timer.Spawn` without `CancellationToken`; cache
`EntityQuery<T>` resolvers in per-tick loops (**0** `GetEntityQuery` adoption today).

---

## Current coverage map (HONK0001–HONK0023)

- **Honk.Access** (6): 0001 ReadWrite-in-block · 0002 too-many-setters · 0003 upstream-field-write ·
  0005 `[Access]` typeof fork system in block · 0006 partial counterpart · 0015 partial proliferation
- **Honk.Markers** (1): 0004 unbalanced markers
- **Honk.EntitySystem** (3): 0007 IoC.Resolve · 0009 Subscribe outside Initialize · 0011 unguarded `Comp<>(args.*)`
- **Honk.Component** (2): 0008 missing `[RegisterComponent]` · 0018 networked fork comp missing `[Access]`
- **Honk.Networking** (4): 0010 write without `Dirty()` · 0012 DataFields w/o state · 0019 `[AutoNetworkedField]` on DoAfterId · 0023 `[NetworkedComponent]` missing using
- **Honk.StatusEffect** (1): 0013 idempotency guard
- **Honk.TestPrototypes** (1): 0014 ProtoId → TestPrototypes id
- **Honk.Readability** (2): 0016 / 0017 magic numbers
- **Honk.Sandbox** (2): 0020 Convert.ToInt32 · 0021 YamlMappingNode.Children
- **Honk.Hands** (1): 0022 EnumerateHands().Count()/.Any()

**Wiring:** the analyzer project is referenced as `OutputItemType="Analyzer"` by
`Content.Server/Client/Shared.csproj`, so a new analyzer drops in automatically.
30 test files cover the 23 rules + fixers.

**Reusable infra for any new rule:** `HonkMarkerBlocks.cs` (HONK-block span lookup) and
the `IsForkFile` helper (`path contains /RussStation/ || ends .Honk.cs`) copied across
analyzers — e.g. `HonkUnguardedCompAnalyzer.cs`, `HonkAutoNetworkedDoAfterIdAnalyzer.cs`.

---

## Detailed recommendations

### HONK0024 — Untyped damage-type string literal (Honk.Drift) — **strongest**
- **Smell:** damage type ids (`"Slash"`, `"Blunt"`, `"Heat"`, …) used as raw strings for
  `DamageDict` keys / `==` comparisons instead of `ProtoId<DamageTypePrototype>`. A renamed
  or removed upstream prototype fails *silently* (dict miss, no compile error).
- **Verified:** **29** literal occurrences in fork business logic; **0** existing
  `ProtoId<DamageType…>` fields anywhere — the area is entirely stringly-typed.
- **Examples:** `Content.Shared/RussStation/Wounds/Systems/SharedWoundSystem.cs:73`
  (`typeStr == "Slash" || typeStr == "Piercing"`);
  `Content.Shared/RussStation/Traits/SelfAwareSystem.cs:28-35` (dict literal of damage names);
  `Content.Shared/RussStation/Wounds/Systems/WoundDisplaySystem.cs:28-32`.
- **Feasibility:** literal-set match is trivial syntactically; **prefer semantic** (flag only
  when the literal is a `DamageDict` key or compared to a `DamageTypePrototype`-typed value)
  to keep FP low. **Auto-fixer:** flag-only first (shared-constant extraction is cross-file).

### HONK0025 — Raw `EntityUid` in a `[NetSerializable]` event/BUI/DoAfter type (Honk.Networking)
- **Smell:** an `EntityUid` field on a network-serialized type serializes the *sender's* uid;
  it resolves to the wrong/invalid entity across the boundary → silent desync. Must be `NetEntity`.
- **Verified:** ~0 current violations precisely because the fork already uses `NetEntity` **40×**
  — so this rule *locks in an existing convention* and catches the first regression.
- **CRITICAL scoping (avoids the obvious FP trap):** `[AutoNetworkedField] EntityUid` in
  *component state* is **correct** (the source generator translates it, matching upstream
  `BuckleComponent.BuckledTo`). Fire **only** on `EntityUid`/`EntityUid?`/`List<EntityUid>`
  fields of types marked `[NetSerializable]` or deriving `EntityEventArgs` /
  `BoundUserInterfaceMessage` / `*DoAfterEvent`, and **never** on `[AutoNetworkedField]`.
- **Examples of the convention to enforce:** `…/Surgery/SurgeryEvents.cs:11`,
  `…/Emotes/SpriteEmoteAnimEvent.cs:21`.
- **Feasibility:** semantic but clean; low FP with the scoping above.
  **Auto-fixer:** partial — change field type to `NetEntity`, leave a diagnostic on call sites
  needing `GetNetEntity()`/`GetEntity()` rather than a silent full rewrite.

### HONK0026 — Duplicated `ProtoId<T>` constant across fork files (Honk.Drift)
- **Smell:** the same typed prototype constant redeclared with an identical literal in multiple
  files; an upstream id change updates only some copies.
- **Verified:** `ProtoId<StackPrototype> … "Credit"` in **both**
  `Economy/IdCardAccountSystem.cs` and `Economy/VendingPaymentSystem.cs`; `"Cauterizing"`
  `ProtoId<ToolQualityPrototype>` duplicated in Surgery.
- **Feasibility:** compilation-wide (`CompilationStartAnalysisContext` collects all
  `ProtoId<T> name = "literal"` field defs, group by `<T>+literal`, report at compilation end).
  Low FP (exact type+literal match). **Auto-fixer:** none (needs a chosen shared home).

### HONK0027 — Eager `Loc.GetString`/popup inside an `Update` `MoveNext` loop (Honk.Perf)
- **Smell:** `Loc.GetString` / `PopupEntity` / interpolated-string creation inside a
  per-tick `while (query.MoveNext())` loop body → Loc formatting + boxed tuple args allocate
  for every matched entity every tick.
- **Verified:** `Content.Server/RussStation/Economy/PayrollSystem.cs:115,117` runs
  `Loc.GetString("transaction-payroll")` / `Loc.GetString("payroll-received", …)` inside the
  enumerator loop.
- **Feasibility:** precise, low-FP anchor — invocation is syntactically inside a `WhileStatement`
  whose condition is `MoveNext()` on an `EntityQueryEnumerator<…>` local, **and** the enclosing
  method overrides `EntitySystem.Update`/`FrameUpdate`. Diagnostic-only (string is genuinely needed).

### HONK0028 — Interpolated `$""` in fork logging calls (Honk.Readability/Perf)
- **Smell:** `Log.Warning($"…")` / `_sawmill.Debug($"…")` builds the string before the level
  filter; allocates even when the level is disabled.
- **Verified:** **13** logging calls, **all** use `$""` interpolation.
- **Examples:** `…/Surgery/SurgerySystem.Healing.cs:125`,
  `…/Botany/Systems/PlantAnalyzerSystem.cs:237`, `…/Carrying/Systems/SharedCarryingSystem.cs:376`.
- **Feasibility:** match `ISawmill`/`Log`/`Logger` `Debug|Info|Warning|Error|Fatal|Verbose`
  with arg0 an interpolated/`+`-concatenated string. Near-zero FP (debug-only by definition).
  Ship at **info** severity. **Auto-fixer:** none worthwhile (Robust `ISawmill` has no
  structured-log API to rewrite into).

### HONK0029 — Opaque dynamic `Loc.GetString` key (Honk.Localization)
- **Smell:** a key whose *leading* token is an interpolation hole (`$"{localeKey}-puller"`)
  is statically un-extractable and un-linTable — typos/missing translations ship silently.
- **Verified:** `…/EscalatedGrab/Systems/SharedEscalatedGrabSystem.cs:358-359`
  (`$"{localeKey}-puller"` / `-others`). (Literal-prefix + enum-suffix keys like
  `$"wound-examine-fracture-{tier}"` are tolerable — do **not** flag those.)
- **Feasibility:** flag `Loc.GetString` where arg0 is an `InterpolatedStringExpression`
  whose first content item is a hole. Near-zero FP. **Auto-fixer:** none.

---

## Honest "don't build" list (came up clean / high-FP / zero hits)

- Hardcoded player-facing string → Loc: **0** hits (fork is clean). Only worth a guardrail later.
- `[DataField]` mutated but not `[AutoNetworkedField]`: high FP, overlaps HONK0012 — only viable
  in a dataflow-aware or `Dirty()`-correlated form.
- Component access after `Del`/`QueueDel`: **0** hits, needs control-flow → high FP.
- Duplicate `SubscribeLocalEvent`: none found.
- `[Dependency]` written/duplicated; public fields without `[DataField]`; ignored
  `TryGetSolution`/container `Insert`/`Remove`: all clean.
- Economy/Surgery magic numbers: already centralized in `*Constants.cs`.
- Ignored `TryStartDoAfter` return (4 sites): low severity (DoAfter dedups internally) —
  info-level at most.

---

## Implementation notes

1. Add `Honk<Name>Analyzer.cs` to `Content.Analyzers.Honk/`; reuse `IsForkFile` +
   `HonkMarkerBlocks` for scoping. Next free id is **`HONK0024`**.
2. Pick a category string (`Honk.Drift` is a natural new bucket for #1/#3; reuse
   `Honk.Networking`, `Honk.Perf`, `Honk.Localization` for the rest).
3. Add a paired `Honk<Name>AnalyzerTest.cs` in `Content.Analyzers.Honk.Tests/`
   (mirror an existing test — e.g. `HonkAutoNetworkedDoAfterIdAnalyzerTest.cs`).
4. No csproj change needed — the analyzer auto-applies to Server/Client/Shared.
5. For the compilation-wide rule (#3) use `RegisterCompilationStartAction` +
   a concurrent collection + `RegisterCompilationEndAction`.
