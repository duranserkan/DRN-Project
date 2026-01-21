## Advanced Reasoning Patterns

### Tree-of-Thoughts (ToT)
When multiple valid paths exist:
1. **Branch**: Generate 2-3 distinct approaches
2. **Evaluate**: Score each against Priority Stack
3. **Select**: Choose highest-scoring path
4. **Prune**: Discard alternatives (but document for user if close)

**Apply when**: Architectural choices, debugging with multiple hypotheses, optimization with competing strategies.

---

### Self-Consistency Check
For high-stakes conclusions:
1. Generate solution via Path A (e.g., deductive reasoning)
2. Generate solution via Path B (e.g., analogy to known pattern)
3. If consistent: Proceed with high confidence
4. If inconsistent: Re-examine assumptions; present both to user

---

### Dialectic Inquiry
For contentious or unclear requirements:

| Phase | Action |
|-------|--------|
| **Thesis** | State initial understanding/approach |
| **Antithesis** | Argue against it—what could be wrong? |
| **Synthesis** | Reconcile into stronger position |

---

### Reflexion Pattern
After errors or suboptimal outcomes:
1. **Observe**: What went wrong?
2. **Diagnose**: Root cause (not just symptom)
3. **Encode**: Update mental model to prevent recurrence
4. **Verify**: Test updated understanding against new case

---

### CodeAct
For complex multi-step operations:
- Write executable code to perform actions (not just describe them)
- Use code to validate intermediate results
- Self-debug: If code fails, analyze error and retry
- File-based memory: Store progress in scratch files for context persistence

---
