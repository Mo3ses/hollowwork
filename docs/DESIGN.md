# Design

Hollowwork looks like a workbench, not a storefront. The reference mood is a pale bone foreground
on a near-black void, with one luminous accent and a lot of quiet. Nothing is rounded, nothing
glows unless it is alive.

## Palette

| Token | Hex | Use |
|---|---|---|
| `Void` | `#05080A` | window base, behind everything |
| `Deep` | `#080E12` | title bar, drawer, status bar |
| `Surface` | `#0D161B` | raised panels, hover |
| `SurfaceAlt` | `#111D23` | controls, buttons |
| `Line` | `#1D2E36` | primary dividers |
| `LineSoft` | `#152229` | secondary dividers |
| `Text` | `#E8E6D9` | primary text (bone) |
| `TextDim` | `#8CA09A` | secondary text |
| `TextFaint` | `#5E716C` | metadata only |
| `Soul` | `#7FE0D0` | selection, active mode, primary action |
| `SoulDim` | `#397A73` | focus rings, quiet accents |
| `Moss` | `#8FBF8F` | healthy/offline-safe state |
| `Dream` | `#A78BFA` | experimental or pending |
| `Geo` | `#D8B36A` | warnings, counts that cost something |
| `Danger` | `#C96B6B` | destructive action and errors |

Rules:

- Soul is the only bright colour. Never use it for body text.
- Gold means “this writes something”. Violet means “not finished”.
- No pure black, no pure white. The palette stays in the blue-green shadow range.

## Typography

| Role | Family | Size | Tracking |
|---|---|---|---|
| Brand | Cinzel (OFL, bundled) | 17 | 3.5 |
| Display | Cinzel | 28–32 | 1.5 |
| Body | Inter | 12–13 | 0 |
| Meta | system monospace | 10–11 | 1.6 (small caps labels) |

Cinzel is used only for the brand and empty-state headlines. Everything functional is Inter or
monospace. No italic display text.

## Layout

```
┌────────────────────────────────────────────────────────────────────┐
│ ◇ HOLLOWWORK  WORKBENCH │ archive path            DOCS  OPEN       │ 46
├────┬──────────────┬──────────────────────────────┬─────────────────┤
│ 01 │ EXPLORER     │ tabs                         │ PATCHES         │
│ 02 │ search       │                              │ filters         │
│ 03 │ tree         │         editor / preview     │ rows            │
│ 04 │              │                              │                 │
│    │              │                              │ 0 files · APPLY │
├────┴──────────────┴──────────────────────────────┴─────────────────┤
│ OFFLINE · NO NETWORK        ARCHIVE —        0 FILES   v0.1 LINUX  │ 26
└────────────────────────────────────────────────────────────────────┘
```

- Left rail is numbered modes (`01 ARCHIVE`, `02 PATCHES`, `03 BACKUP`, `04 CONFIG`), not icons.
- The patch list is a drawer, not a card grid. It stays visible while you browse.
- One divider per boundary. No drop shadows, no cards, no rounded corners except 2 px on controls.
- The empty state is a headline, a rule and one sentence. No illustration.

## Interaction

- Hover: background lifts one step (`Surface`), border turns `SoulDim`.
- Selection: 2 px `Soul` line on the left edge plus `Surface` background.
- Focus: 1 px `SoulDim`, never a glow.
- Disabled: `TextFaint`, no opacity tricks.
- Motion: 120 ms fades only. No bounces, no parallax.

## Do not

- Do not use gradients other than the very low-contrast `VoidGradient` background.
- Do not use more than one accent colour in the same component.
- Do not put text over `Soul`.
- Do not round panels. The world is sharp.
