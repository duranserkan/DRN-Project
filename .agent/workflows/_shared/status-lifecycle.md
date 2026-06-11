---
description: Shared status lifecycle for the /clarify → /answer → /develop pipeline
---

> **Estimated context: ~0.1K tokens**

## Status Lifecycle

```text
draft → clarifying → draft-self-reviewed → clarified → ready-to-develop → implemented
 └─ /clarify ──────────────────────────────┘  └─ /answer ──────────────┘  └─ /develop ─┘
```

### Status Transitions

| Status | Trigger | Owner |
|---|---|---|
| `draft` | Document created (§2) | `/clarify` |
| `clarifying` | First question round begins (§5) | `/clarify` |
| `draft-self-reviewed` | Gates and checklist pass (§9) | `/clarify` |
| `clarified` | Approval criteria met (§6) | `/answer` |
| `ready-to-develop` | `DEVELOP-*.md` produced (§7) | `/answer` |
| `implemented` | User approves final report (§7b) | `/develop` |

### Re-entry
Resume from the last incomplete step identified by the document's `status` field.
