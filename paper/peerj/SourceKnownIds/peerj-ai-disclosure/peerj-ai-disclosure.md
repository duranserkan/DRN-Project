# AI Disclosure for PeerJ Submission

## Overview

Documents mentioned in **Directory Structure** section below contains the required AI disclosure materials for the PeerJ journal submission of the **Source Known Identifiers: A Three-Tier Identity System for Distributed Applications** paper. The disclosure documents the use of an AI-powered code review tool during the development of the secure SKEID variant.

## AI Tool Used

| **Attribute** | **Detail**                                                                    |
|---------------|-------------------------------------------------------------------------------|
| Tool          | https://coderabbit.ai                                                         |
| Purpose       | Automated code review                                                         |
| Role          | Review-only, the AI did not write, generate, or edit any code                 |
| Configuration | Configured by `.coderabbit.yaml` that contains author-provided review prompts |
| How it works  | It automatically analyzes pull request diffs and posts review comments        |
| Review date   | February 21, 2026                                                             |

### Important

**CodeRabbit was used exclusively as a code reviewer, not as a code generator.** CodeRabbit analyzed the submitted code changes and provided feedback in the form of inline review comments on the GitHub pull request. The author then independently decided which feedback to incorporate in a subsequent commit.

## Directory Structure

```
peerj-ai-disclosure/
├── peerj-ai-disclosure.pdf ← This file
├── code-before-ai-review-SourceKnownEntityIdUtils.cs ← Code at commit 3a8c95f
├── code-after-ai-review-SourceKnownEntityIdUtils.cs ← Code at commit 7fc90b7 
└── .coderabbit.yaml ← The AI tool configuration (contains prompts for CodeRabbit AI)
```

## Timeline

1. **Commit `0248e1a`**: Author writes the initial implementation (AES-256 encrypted SKEID variant) in `feature/secure-source-known-entity-ids` branch
2. **Commit `3a8c95f`**: Author improves XML Summary for `Generate` method in `SourceKnownEntityIdUtils.cs` file
3. **CodeRabbit reviews**: AI tool automatically analyzes the PR and posts 9 review comments
4. **Commit `7fc90b7`**: Author refactors `SourceKnownEntityIdUtils.cs`
5. **PR merged**: Author merges `feature/secure-source-known-entity-ids` branch into `develop` branch with commit `5ba0166`

## Public References

- **Repository**: https://github.com/duranserkan/DRN-Project
- **Pull Request**: https://github.com/duranserkan/DRN-Project/pull/12
- **CodeRabbit Review**: Visible in the PR conversation tab (9 inline comments + 1 summary comment)