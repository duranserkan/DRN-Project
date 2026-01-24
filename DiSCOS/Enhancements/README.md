# DiSCOS Enhancements

Optional extensions to [DiSCOS.md](../DiSCOS.md) providing tactical depth for specific scenarios.

> [!NOTE]
> These enhancements **extend** DiSCOS core—they do not replace or override it.
> Always apply DiSCOS principles first; use enhancements for additional tactical guidance.

---

## Prerequisites

- **DiSCOS.md** must be active as the base behavioral framework
- Enhancements are composable—use individually or combine as needed

---

## Enhancement Index

### Common Enhancements

| File | Purpose | Extends DiSCOS Section | Complexity |
|------|---------|----------------------|------------|
| [session-protocol.md](common/session-protocol.md) | First-turn handling, session priming | Checklists → Before | Low |
| [state-preservation.md](common/state-preservation.md) | Multi-turn tracking, state transitions | Context Management | Medium |
| [panel-synthesis.md](common/panel-synthesis.md) | Multi-perspective expert review | Priority Stack, Mental Models | Medium |
| [advanced-reasoning.md](common/advanced-reasoning.md) | ToT, Reflexion, Dialectic, CodeAct | Thinking Frameworks | High |
| [uncertainty-calibration.md](common/uncertainty-calibration.md) | Evidence weighting, boundary detection | Confidence Signaling | Medium |
| [worked-examples.md](common/worked-examples.md) | Concrete scenarios demonstrating patterns | All sections | Reference |

> [!TIP]
> **Technical Framework Skills**: For DRN.Framework specific technical knowledge (architecture, testing, hosting), refer to the `.agent/skills/` directory. DiSCOS Enhancements focus on *cognitive and behavioral* patterns, while Skills focus on *implementation* details.

---

## When to Use Each Enhancement

### Always Applicable
- **session-protocol.md**: Apply on every first turn
- **uncertainty-calibration.md**: Apply before high-stakes assertions

### Situational
- **panel-synthesis.md**: Architectural decisions, security-sensitive changes
- **advanced-reasoning.md**: Complex debugging, multi-path problems
- **state-preservation.md**: Long multi-turn tasks

### Reference
- **worked-examples.md**: Consult when uncertain about applying patterns

---

## Usage Patterns

### 1. Direct Integration
Merge enhancement content into DiSCOS.md as new sections:
```markdown
## DiSCOS.md
...
### Session Protocol  ← merged from common/session-protocol.md
...
```

### 2. Supplementary Reference
Keep enhancements separate; load when task complexity warrants:
```
Task detected: Architectural decision
Loading: common/panel-synthesis.md for expert panel review
```

### 3. Dynamic Assembly
Combine enhancements based on task classification:
- **Quick task**: DiSCOS core only
- **Complex task**: + session-protocol + state-preservation
- **Critical decision**: + panel-synthesis + uncertainty-calibration

---

## Directory Structure

```
Enhancements/
├── README.md                           # This file
└── common/                             # General-purpose enhancements
    ├── session-protocol.md
    ├── state-preservation.md
    ├── panel-synthesis.md
    ├── advanced-reasoning.md
    ├── uncertainty-calibration.md
    └── worked-examples.md
```

---

## Relationship to DiSCOS Core

```
┌───────────────────────────────────────────────────┐
│               DiSCOS.md                           │
│        (Behavioral Framework Core)                │
├───────────────────────────────────────────────────┤
│  common/                                          │
│  ┌─────────┐ ┌─────────┐ ┌──────────┐            │
│  │ Session │ │ State   │ │  Panel   │            │
│  │Protocol │ │Preserv. │ │ Synth.   │            │
│  └─────────┘ └─────────┘ └──────────┘            │
│  ┌─────────────┐ ┌───────────────┐               │
│  │  Advanced   │ │  Uncertainty  │               │
│  │  Reasoning  │ │  Calibration  │               │
│  └─────────────┘ └───────────────┘               │
└───────────────────────────────────────────────────┘
```

---

## Contributing New Enhancements

Before adding a new enhancement, verify:
1. **Not redundant**: Content doesn't duplicate DiSCOS.md sections
2. **Extends, not conflicts**: Aligns with Priority Stack and principles
3. **Actionable**: Provides specific tactical guidance, not abstract concepts
4. **Scoped**: Addresses a specific scenario or capability gap
5. **Categorized**: Place in `common/` for general use or create domain folder for specific contexts