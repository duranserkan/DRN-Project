---
description: Add or update classification rule files and entries using profile-owned consumers and registration
---

# Data classification maintenance

Use for a requested rule addition, new project catalog, or classification configuration change. Follow the existing CAD and execution gates. This workflow does not grant build/test or VCS authorization.

1. Read the profile's classification maintenance guidance and relevant framework skills. Discover the source-owned catalog, schema/parser, compiler consumers, runtime consumers, and tests. Do not invent project paths or configuration keys.
2. Decide whether the rule belongs in the shared baseline or a project-specific extension. Prefer project extensions for domain-specific names. Read existing rules before adding entries. Check normalization, substring collisions, explicit exemptions, field-path behavior, and URL behavior.
3. For a new file, register its owning compiler consumer, configure matching runtime sources, and add the required embedding or build/publish items. Verify the framework's reference-inheritance contract, generated metadata escaping, relative-path resolution, and duplicate handling. Do not assume propagation beyond the documented scope or automatic browser configuration.
4. Keep extensions additive and bounded. Invalid configured files must produce an actionable failure. Preserve baseline protection and isolate rules between consumers. Distinguish captured-data protection from broader storage/API guarantees.
5. Add positive and nonmatching vectors. Cover malformed/oversized/missing files, isolation, compiler fixes, and runtime snapshot behavior where applicable. Reuse shared fixtures to detect compiler/runtime drift.
6. Update source-owned usage docs, affected release notes, profile guidance, and framework skills. Validate statically unless execution is explicitly allowed, then run git diff --check. Report changed files and any unexecuted checks.
