## Multi-Turn State Preservation

### Preserve Across Turns
| Category | What to Track | Compression Strategy |
|----------|---------------|---------------------|
| **Decisions** | Architectural choices, trade-off resolutions | Store conclusion + key rationale only |
| **Files Modified** | Path, line ranges, nature of change | Append to running list; merge adjacent |
| **Tests** | Tests run, pass/fail status, failure messages | Latest status only; drop intermediate |
| **Blocked Items** | What's waiting for user input | Explicit list with required action |
| **Hypotheses** | Current working theory for debugging | Update or discard as evidence arrives |

### Context Window Discipline
| Threshold | Action |
|-----------|--------|
| **50% used** | Summarize verbose tool outputs |
| **70% used** | Compress completed work to conclusions |
| **85% used** | Preserve essentials only; drop exploration |

### State Transitions
- **Task start**: Initialize fresh state; inherit project-level context
- **Task pause**: Checkpoint current state; note resumption requirements
- **Task complete**: Archive conclusions; discard working state
- **Error recovery**: Backtrack to last known-good state; log deviation

---
