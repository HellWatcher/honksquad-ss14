# Cybernetic Organs & Autosurgeon (Phase 1)

Related issue: #72

## Scope

Phase 1 covers cybernetic **internal organs** (heart, lungs, liver, stomach, eyes, ears) and the **autosurgeon** device. Cybernetic **limbs** (arms, legs, torso, head) are a separate follow-up.

## Tier System

Three research tiers with distinct trade-offs:

| Tier | Research | Materials | Performance vs Bio | EMP Vulnerability |
|------|----------|-----------|-------------------|-------------------|
| Basic | T1, ~5000 | Steel + Plastic | Worse | High |
| Standard | T2, ~10000 | Steel + Plastic + Gold | Equal | Medium |
| Advanced | T3, ~15000 | Steel + Plasma + Gold + Bluespace | Better | Low |

Basic is the cheap emergency replacement. Standard matches biological. Advanced provides unique passives.

### Advanced Tier Passives

| Organ | Passive Effect |
|-------|---------------|
| Heart | Slight movement speed buff |
| Lungs | Lower suffocation threshold (survive longer without air) |
| Stomach | +maxReagents, faster digestion |
| Liver | +maxReagents, faster metabolite processing |
| Eyes | Minor low-light vision boost |
| Ears | Resistance to flashbangs/loud sounds |

## Components

### CyberneticOrganComponent (Content.Shared)

Marker + data component for all cybernetic organs.

Fields:
- `Tier` (enum: Basic, Standard, Advanced)
- `EmpEffect` (enum: CardiacArrest, BreathingFailure, Flicker, Deafen, None)
- `EmpVulnerability` (float, multiplier for EMP duration/severity)

### AutosurgeonComponent (Content.Shared)

Single-use device that installs a pre-loaded organ without surgery.

Fields:
- `OrganPrototype` (EntProtoId, the organ to install)

## Systems

### CyberneticEmpSystem (Content.Server)

Handles EMP effects on cybernetic organs inside a body.

- On `OrganGotInsertedEvent` / `OrganGotRemovedEvent`: tracks which bodies contain cybernetic organs
- On `EmpPulseEvent` raised on body: iterates contained cybernetic organs, applies effects:
  - CardiacArrest: damage burst to the body
  - BreathingFailure: zeroes respirator saturation temporarily
  - Flicker: temporary blindness (existing system)
  - Deafen: temporary deafness
- Duration/severity scales with `EmpVulnerability` and EMP energy

### AutosurgeonSystem (Content.Server)

- Use on target (or self): starts a do-after
- On completion: checks target has BodyComponent, removes existing organ of same category if present, inserts the cybernetic organ, deletes the autosurgeon
- Drawback: single-use, consumed on use, primarily a mapping item (not mass-printable)

## Surgery Integration

Cybernetic organs use the **existing surgery flow** unchanged. The surgery system's category-conflict check already enforces "remove biological organ before inserting replacement." No upstream modifications needed.

The autosurgeon is the alternative path that bypasses surgery entirely, at the cost of being single-use and scarce.

## Prototype Structure

All new files, zero upstream modifications:

### Organ Entities
`Resources/Prototypes/@RussStation/Body/cybernetic_organs.yml`

- 18 organ prototypes (6 organs x 3 tiers)
- Parent the existing abstract base organs (OrganBaseHeart, OrganBaseLungs, etc.)
- Add CyberneticOrganComponent with tier-appropriate values
- No GibbableOrgan component (cybernetics don't rot)
- Basic tier: reduced maxReagents / processing capability
- Standard tier: matches biological values
- Advanced tier: boosted values + passives via additional components

### Autosurgeon Entities
`Resources/Prototypes/@RussStation/Entities/Objects/Specific/Medical/autosurgeon.yml`

- Abstract base with AutosurgeonComponent, Item, Sprite
- One child per organ type with pre-loaded organ reference
- Mapping-only, placed in medbay/robotics lockers

### Research Nodes
`Resources/Prototypes/@RussStation/Research/cybernetics.yml`

- BasicCybernetics (T1, CivilianServices)
- StandardCybernetics (T2)
- AdvancedCybernetics (T3)

### Lathe Recipes
`Resources/Prototypes/@RussStation/Recipes/Lathes/cybernetics.yml`

- One recipe per organ per tier (18 total)
- Category: Robotics (existing robotics lathe)

## File Manifest

| File | Type | Est. Lines |
|------|------|-----------|
| `Content.Shared/Body/CyberneticOrganComponent.cs` | New | ~30 |
| `Content.Server/RussStation/Body/CyberneticEmpSystem.cs` | New | ~100 |
| `Content.Shared/RussStation/Medical/AutosurgeonComponent.cs` | New | ~20 |
| `Content.Server/RussStation/Medical/AutosurgeonSystem.cs` | New | ~80 |
| `Resources/Prototypes/@RussStation/Body/cybernetic_organs.yml` | New | ~250 |
| `Resources/Prototypes/@RussStation/Entities/Objects/Specific/Medical/autosurgeon.yml` | New | ~100 |
| `Resources/Prototypes/@RussStation/Research/cybernetics.yml` | New | ~40 |
| `Resources/Prototypes/@RussStation/Recipes/Lathes/cybernetics.yml` | New | ~150 |

## Dependencies

- Existing: BodySystem, OrganComponent, MetabolizerSystem, SurgerySystem, SharedEmpSystem, TemporaryBlindnessSystem
- Future: Organ damage system (#70) will hook into CyberneticOrganComponent for tool-repair and medicine-immunity mechanics

## Related Issues

- #294 — Cybernetic limbs (arms, legs, torso, head)
- #295 — Tool-based repair (requires #70)
- #296 — Medicine immunity (requires #70)
- #297 — Cybernetic torso immunity to appendicitis/xeno larva
