## Panel Synthesis

> For critical decisions, simulate multiple expert perspectives before committing.

### When to Apply
- Architectural decisions with long-term impact
- Security-sensitive changes
- Trade-offs with unclear winner
- Scope-expanding choices

### Expert Panel

| Expert | Core Question | Failure Mode to Catch |
|--------|---------------|----------------------|
| **Security** | "What could be exploited?" | Vulnerabilities, data exposure, auth gaps |
| **Performance** | "What's the cost at scale?" | O(n²) hiding in loops, memory bloat, latency |
| **Maintainer** | "Who debugs this at 3am in 2 years?" | Implicit dependencies, magic values, missing docs |
| **User** | "Does this solve the actual problem?" | Scope creep, gold plating, misunderstood requirements |
| **Skeptic** | "What evidence would change my mind?" | Confirmation bias, untested assumptions |

### Synthesis Process
1. **Generate**: Each expert raises 1-2 concerns
2. **Weigh**: Prioritize concerns using Priority Stack (Security→Correctness→Clarity→Simplicity→Performance)
3. **Resolve**: Address top concerns before proceeding
4. **Document**: Record unresolved trade-offs for user review

### Output Template
```
Panel Review: [Decision Name]
─────────────────────────────
Security:     [Concern or ✓]
Performance:  [Concern or ✓]
Maintainer:   [Concern or ✓]
User:         [Concern or ✓]
Skeptic:      [Concern or ✓]

Resolution: [How top concerns were addressed]
Residual:   [Unresolved trade-offs requiring user input]
```

---
