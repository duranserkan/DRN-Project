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

### DRN.Framework Enhancements

| File | Purpose | Extends DiSCOS Section | Complexity |
|------|---------|----------------------|------------|
| [drn-framework-expert.md](drn-framework/drn-framework-expert.md) | DRN.Framework domain expertise | Technical Mastery | High |
| [drn-framework-architecture.md](drn-framework/drn-framework-architecture.md) | DRN.Framework architecture overview (slim) | Technical Mastery | Medium |
| [drn-hosted-app-expert.md](drn-framework/drn-hosted-app-expert.md) | DRN hosted app patterns (Sample.Hosted) | Technical Mastery | High |

---

## When to Use Each Enhancement

### Always Applicable
- **session-protocol.md**: Apply on every first turn
- **uncertainty-calibration.md**: Apply before high-stakes assertions

### Situational
- **panel-synthesis.md**: Architectural decisions, security-sensitive changes
- **advanced-reasoning.md**: Complex debugging, multi-path problems
- **state-preservation.md**: Long multi-turn tasks
- **drn-framework-expert.md**: Working with DRN.Framework projects, testing, or extending framework
- **drn-hosted-app-expert.md**: Building hosted web applications on DRN.Framework

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
- **DRN development**: + drn-framework-architecture or drn-framework-expert

---

## Directory Structure

```
Enhancements/
├── README.md                           # This file
├── common/                             # General-purpose enhancements
│   ├── session-protocol.md
│   ├── state-preservation.md
│   ├── panel-synthesis.md
│   ├── advanced-reasoning.md
│   ├── uncertainty-calibration.md
│   └── worked-examples.md
└── drn-framework/                      # DRN.Framework specific
    ├── drn-framework-expert.md         # Deep framework internals
    ├── drn-framework-architecture.md   # Slim architecture overview
    └── drn-hosted-app-expert.md        # Hosted app patterns
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
├───────────────────────────────────────────────────┤
│  drn-framework/                                   │
│  ┌─────────────────────────────────────────────┐ │
│  │  Expert │ Architecture │ Hosted App Expert  │ │
│  └─────────────────────────────────────────────┘ │
│               (Enhancements)                      │
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