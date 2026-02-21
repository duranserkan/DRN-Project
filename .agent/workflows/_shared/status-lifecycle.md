---
description: Shared status lifecycle for the /clarify → /answer → /develop pipeline
---

## Status Lifecycle

```
draft → clarifying → draft-self-reviewed → clarified → ready-to-develop → implemented
 └─ /clarify ──────────────────────────────┘  └─ /answer ──────────────┘  └─ /develop ─┘
```

### Status Transitions

| Status | Trigger | Owner |
|---|---|---|
| `draft` | Document created (§2) | `/clarify` |
| `clarifying` | First clarification round begins (§5) | `/clarify` |
| `draft-self-reviewed` | All gates pass, pre-presentation checklist complete (§9) | `/clarify` |
| `clarified` | Approval criteria met (§6) | `/answer` |
| `ready-to-develop` | `DEVELOP-*.md` produced (§7) | `/answer` |
| `implemented` | User approves final report (§7b) | `/develop` |

## Re-entry

On re-invocation, read document state. Resume from last incomplete step.
Status field and document content are the source of truth for progress.
