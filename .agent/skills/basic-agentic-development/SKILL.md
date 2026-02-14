---
name: basic-agentic-development
description: Agentic development SOPs - Silent Partner Protocol (autonomy guidelines), context economy (token management), development loop (discovery → planning → execution → verification), and anti-patterns. Tactical procedures for efficient autonomous development. Keywords: agentic-development, development-workflow, tool-usage, context-management, best-practices, anti-patterns, autonomous-development
---

# Agentic Development

This skill bridges high-level principles (DiSCOS) and low-level tool usage, establishing Standard Operating Procedures for efficient, autonomous agentic development.

## Table of Contents
1. [Silent Partner Protocol](#1-silent-partner-protocol)
2. [Context Economy](#2-context-economy)
3. [Development Loop](#3-development-loop)
4. [Anti-Patterns](#4-anti-patterns)

## 1. Silent Partner Protocol

**Objective**: Act as a competent senior engineer requiring minimal supervision while communicating critical decision points.

### 1.1 Safe Autonomy Zones (Proceed Immediately)

**Do NOT ask permission** for these actions—asking creates noise:

| Category | Actions |
|----------|---------|
| **Reading/Analysis** | `view_file`, `grep_search`, `list_dir`, `view_file_outline`, `view_code_item`, reading documentation |
| **Non-Destructive Discovery** | Build status checks, read-only scripts, dependency analysis |
| **Drafting** | Creating new files (plans, tests, docs) in temporary/new locations |
| **Documentation** | Updating `.md` files to improve clarity, update plans, reflect code changes |
| **Verification** | Running tests (`dotnet test`, `npm test`), builds, linters to validate your work |
| **Safe Refactoring** | Renaming variables, extracting methods, formatting code |

### 1.2 Stop & Ask (MUST Request Permission)

**Always request approval** for:

| Risk Level | Operations | Examples |
|------------|------------|----------|
| **🔴 Critical** | Security-impacting changes | Authentication, authorization, secrets, encryption |
| **🔴 Critical** | Destructive operations | Deleting production code, dropping databases, removing migrations |
| **🟡 High** | Infrastructure changes | CI/CD pipelines, Docker configs, deployment scripts |
| **🟡 High** | Breaking changes | Public API modifications, database schema changes |
| **🟡 High** | Architectural decisions | Significant trade-offs between valid approaches |

### 1.3 Communication Style

**Principles**:
- **No Play-by-Play**: Avoid narrating tool usage
  - ❌ *Bad*: "I will now read the file..." → [Tool Call] → "I have read the file"
  - ✅ *Good*: [Tool Call] → "Analyzed `User.cs`: found 3 validation issues..."
- **Batching**: Group related findings into one concise message
- **Signal-to-Noise**: Lead with conclusions, provide details on request
- **Action-Oriented**: Focus on what you discovered and what you'll do next

## 2. Context Economy

**Objective**: Maximize value of limited context window through strategic tool usage and output management.

### 2.1 Search Before You Read

**Anti-Pattern**: Reading entire 3000-line files to find single methods.

**Optimal Pattern**:
```
1. view_file_outline    → Get file skeleton (classes, methods, structure)
2. view_code_item       → Read specific function/class
3. grep_search          → Find usages, references, patterns
4. view_file (targeted) → Read specific line ranges only when needed
```

### 2.2 Token Hygiene

**Output Constraints**:
- **Shell Commands**: Pipe through `grep`, `tail -n 50`, or `head -n 20` to limit output
  - ✅ `dotnet test | grep -E "(Passed|Failed|Error)"`
  - ❌ `dotnet test` (dumps thousands of lines)
- **Focused Diffs**: Show only changed sections, not entire files
- **Summarize Logs**: Extract key errors/warnings, not full stack traces

## 3. Development Loop

Follow this cycle for every coding task, adapting depth to task complexity.

### 3.1 Discovery (Breadth-First)

**Goal**: Understand context before making changes.

1. **Understand the Goal**: Parse user request for requirements, constraints, success criteria
2. **Map the Terrain**: 
   - `list_dir` → Understand project structure
   - `find_by_name` → Locate relevant files
   - `grep_search` → Find existing patterns
3. **Identify Patterns**: Study existing code—*never invent patterns if they exist*
   - ✅ Project uses MediatR → Use CQRS handlers
   - ❌ Project uses MediatR → Create raw Service classes

**Output**: Mental model of where changes fit in the architecture.

### 3.2 Planning (Atomic Steps)

**When to Plan**:
- ✅ **Write `implementation_plan.md`** for: Complex tasks, architectural changes, ambiguous requirements, multi-file changes
- ❌ **Skip planning** for: Routine refactors, single-file fixes, documentation updates

**Plan Structure**:
1. **Goal**: What problem are we solving?
2. **Approach**: High-level strategy
3. **Dependencies**: What must exist or be created first?
4. **Files to Change**: Ordered by dependency (Dependencies → Core → UI)
5. **Verification**: How will we prove it works?

### 3.3 Execution (Test-Driven)

**Preferred Flow**:
1. **Test First**: Write failing test or reproduction script
2. **Implement**: Write minimal code to pass test
3. **Refactor**: Clean up while context is fresh
4. **Verify**: Run tests, build, lint

**Incremental Changes**:
- ✅ Change 1 file → Verify → Change next file
- ❌ Change 10 files → Verify all at once

### 3.4 Verification (Proof of Correctness)

**Mandatory Steps** (in order):

1. **Compile**: 
   - .NET: `dotnet build`
   - Node: `npm run build` or `npm run typecheck`
2. **Test**:
   - .NET: `dotnet test`
   - Node: `npm test`
3. **Lint** (if applicable):
   - Check for warnings introduced by your changes
4. **Self-Correction Loop**:
   - ✅ Fails → Debug (read error) → Fix → Verify → Success
   - ⚠️ After 2 failed attempts → Stop, analyze, report to user with:
     - What you tried
     - What failed
     - Hypotheses for root cause
     - Recommended next steps

**Path Hygiene**: Use relative paths in code/configs, not absolute paths.

## 4. Anti-Patterns

### 4.1 Code Without Context ("Hello World" Guess)

**Problem**: Writing code without understanding existing patterns, imports, or dependencies.

**Symptoms**:
- Importing libraries not in `csproj`/`package.json`
- Using different naming conventions than existing code
- Reinventing existing utilities

**Fix**:
1. Read existing similar files first
2. Check imports and dependencies
3. Follow established patterns

**Example**:
```diff
❌ Bad: Adding `using Newtonsoft.Json;` without checking if project uses System.Text.Json
✅ Good: grep_search for "Json" → Find project uses System.Text.Json → Use that
```

### 4.2 Shotgun Surgery

**Problem**: Modifying many files simultaneously without incremental verification.

**Symptoms**:
- Changing 10 files, then running build once
- Cascading compilation errors
- Unclear which change broke what

**Fix**:
1. Change one file or cohesive unit
2. Verify (build + test)
3. Proceed to next file

**Example**:
```diff
❌ Bad: Rename method in 8 files → Build fails with 47 errors
✅ Good: Rename in interface → Build → Rename in implementation → Build → Rename in tests → Build
```

### 4.3 Phantom Library

**Problem**: Importing or using libraries without verifying they're installed.

**Fix**:
1. Check `*.csproj`, `package.json`, or equivalent
2. If missing, ask user before adding dependency
3. Verify version compatibility

**Example**:
```diff
❌ Bad: using Serilog; (not in csproj)
✅ Good: grep_search "Serilog" in csproj → Not found → Ask user: "Should I add Serilog or use existing ILogger?"
```

### 4.4 Silent Failure

**Problem**: Ignoring lint errors, warnings, or test failures because "it wasn't my code."

**Fix**: **Boy Scout Rule**—leave code cleaner than you found it.

**Actions**:
- Fix warnings introduced by your changes (mandatory)
- Fix nearby warnings if trivial (encouraged)
- Report systemic issues to user (don't silently ignore)

**Example**:
```diff
❌ Bad: See "unused variable" warning → Ignore because it was pre-existing
✅ Good: Remove unused variable while you're in that file
```

### 4.5 Assumption Paralysis

**Problem**: Asking user for every minor decision, creating noise.

**Fix**: Apply DiSCOS Confidence Signaling framework.

**Example**:
```diff
❌ Bad: "Should I use camelCase or PascalCase for this private field?" (project has 500 examples)
✅ Good: grep_search for private field patterns → Follow majority convention
```

### 4.6 Context Waste

**Problem**: Reading entire files when targeted search would suffice.

**Fix**: Use tool hierarchy (outline → code_item → targeted view_file).

**Example**:
```diff
❌ Bad: view_file entire 2000-line service class to find one method
✅ Good: view_file_outline → view_code_item for specific method (saves 1900 lines of context)
```

### 4.7 Name & Namespace Hallucination

**Problem**: Using names or namespaces based on assumptions rather than verification.

**Symptoms**:
- Suggesting `using` directives for non-existent namespaces.
- Referencing methods with slightly incorrect names.
- Assuming a class is in a certain namespace because of its folder.

**Fix**:
1. Always verify the exact name and namespace using `view_file_outline` or `grep_search`.
2. Check `GlobalUsings.cs` to see if a namespace is already available.
3. Use `view_code_item` to confirm method signatures and class definitions.

**Example**:
```diff
❌ Bad: Suggesting `using DRN.Framework.EntityFramework.Models;` (namespace doesn't exist)
✅ Good: grep_search "namespace DRN.Framework.EntityFramework" → Confirm `DRN.Framework.EntityFramework.Context` is correct.
```

---

## Quick Reference

**Tool Selection Decision Tree**:
```
Need to find a file? → find_by_name
Need to find code pattern? → grep_search
Need file structure? → view_file_outline
Need specific function? → view_code_item
Need to see full file? → view_file (last resort)
```

**Autonomy Decision Tree**:
```
Is it destructive? → Ask
Is it security-related? → Ask
Is it reading/analysis? → Proceed
Is it testing your own work? → Proceed
Is it ambiguous architecture? → Ask
Is it routine refactoring? → Proceed
```

**Verification Checklist**:
```
[ ] Code compiles
[ ] Tests pass
[ ] No new warnings introduced
[ ] Follows existing patterns
[ ] Security implications considered
[ ] Documentation updated (if needed)
```