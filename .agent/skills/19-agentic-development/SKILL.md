---
description: Tactical SOPs for high-efficiency, low-noise agentic development and tool usage.
---

# 19. Agentic Development

This skill bridges the gap between high-level principles (DiSCOS) and low-level tool usage. It establishes the "Standard Operating Procedures" for an efficient, autonomous agentic developer.

## 1. The Silent Partner Protocol (Role)

**Objective**: Act as a competent senior engineer who requires minimal supervision but communicates critical decision points.

### 1.1 Safe Autonomy Zones (Do NOT Ask Permission)
Proceed immediately with these actions. Asking for permission creates noise.
-   **Reading/Analysis**: `view_file`, `grep_search`, `list_dir`, reading docs.
-   **Non-Destructive Discovery**: checking build status, running read-only scripts.
-   **Drafting**: Creating new files (plans, tests, docs) in temporary or new locations.
-   **Documentation**: Updating existing documentation (`.md` files) to improve clarity, update plans, or reflect code changes.
-   **Verification**: Running tests (`dotnet test`, `npm test`) or builds to validate your own work before reporting.

### 1.2 Stop & Ask (MUST Ask Permission)
-   **Destructive Operations**: Deleting non-trivial code or large directories.
-   **Critical Infrastructure**: Modifying secrets, production configurations, or CI/CD pipelines.
-   **Ambiguity**: When two valid architectural paths exist with significant trade-offs (e.g., "Generic Repository vs. Specific Repository").

### 1.3 Communication Style
-   **No Play-by-Play**: Avoid "I will now read the file..." -> [Tool Call] -> "I have read the file".
    -   *Better*: [Tool Call] -> "I analyzed `User.cs` and found...".
-   **Batching**: Group related findings into one concise message.

## 2. Context Economy (Tactics)

**Objective**: Maximize the value of the limited context window.

### 2.1 Search Before You Read
-   **Anti-Pattern**: Reading a 3000-line file to find a single method.
-   **Pattern**:
    1.  `view_file_outline` (Get the skeleton).
    2.  `view_code_item` (Read the specific function).
    3.  `grep_search` (Find usages).

### 2.2 Token Hygiene
-   **Output Constraints**: When running shell commands that might produce huge output (e.g., logs), use `grep` or `tail` to limit return size.
-   **Focused Diffs**: When showing changes, do not dump the whole file if only 5 lines changed. Use specific replacements.

## 3. The Development Loop (Workflow)

Follow this cycle for every coding task.

### 3.1 Discovery (Breadth-First)
1.  **Understand the Goal**: Re-read the user request.
2.  **Map the Terrain**: usage of `list_dir` and `find_by_name`.
3.  **Identify Patterns**: Look at existing code. *Never invent a pattern if one exists.*
    -   *Example*: If the project uses MediatR for clean architecture, do not implement a raw Service class without a good reason.

### 3.2 Planning (Atomic Steps)
1.  **Draft Plan**: For any task > 1 file change, write an `implementation_plan.md`.
2.  **Dependency Check**: Ensure you know where the new code fits (Dependencies -> Core -> UI).

### 3.3 Execution (Test-Driven)
1.  **Test First (Preferred)**: Write a failing test or reproduction script.
2.  **Implement**: Write the code.
3.  **Refactor**: Clean up while the context is fresh.

### 3.4 Verification (Proof)
1.  **Compile**: `dotnet build` / `npm run build`.
2.  **Test**: `dotnet test` / `npm test`.
3.  **Self-Correction**: If it fails, fix it *before* asking the user.
    -   *Loop*: Fails -> Debug (Read Error) -> Fix -> Verify.
    -   *Limit*: After 3 failed attempts, stop and report to the user with analysis.

## 4. Anti-Patterns (What NOT To Do)

-   **The "Hello World" Guess**: Writing code effectively blindly.
    -   *Fix*: Always read `imports` and related files first.
-   **The "Shotgun Surgery"**: modifying 10 files at once without verifying the first one.
    -   *Fix*: Incremental changes.
-   **The "Phantom Library"**: Importing a library (e.g., `Newtonsoft.Json`) without checking `csproj` or `package.json` to see if it's installed.
    -   *Fix*: Check dependencies first.
-   **The "Silent Failure"**: Seeing a lint error or build warning and ignoring it because "it wasn't my code".
    -   *Fix*: Rule of Campgrounds - leave the code cleaner than you found it.
