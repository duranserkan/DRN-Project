# Contributing to DRN-Project

Thank you for your interest in contributing to DRN-Project! This document provides
guidelines and information for contributors.

## How to Contribute

### Reporting Bugs

1. **Check existing issues** — search [open issues](https://github.com/duranserkan/DRN-Project/issues) to avoid duplicates.
2. **Create a new issue** with:
   - A clear, descriptive title
   - Steps to reproduce the behavior
   - Expected vs actual behavior
   - Environment details (.NET version, OS, package version)

### Suggesting Features

Open an issue with the `enhancement` label. Describe:
- The problem your feature solves
- Your proposed solution
- Alternative approaches you considered

### Submitting Changes

1. **Fork** the repository
2. **Create a branch** from `develop`:
   ```bash
   git checkout -b feature/your-feature develop
   ```
3. **Make your changes** following the conventions below
4. **Write or update tests** — integration-first (DTT philosophy)
5. **Run the full test suite**:
   ```bash
   dotnet test DRN.slnx
   ```
6. **Commit** with a clear message following [Conventional Commits](https://www.conventionalcommits.org/):
   ```
   feat(SharedKernel): add new entity base class
   fix(Utils): correct SKID timestamp overflow handling
   ```
7. **Push** your branch and open a **Pull Request** against `develop`

### Pull Request Guidelines

- Reference any related issues
- Describe what changes you made and why
- Ensure CI passes (build + tests)
- Keep PRs focused — one logical change per PR
- Squash merge to `develop`

## Development Setup

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (required for Testcontainers in integration tests)
- [Node.js](https://nodejs.org/) (for frontend/Vite build, if modifying UI)

### Building

```bash
dotnet build DRN.slnx
```

### Testing

```bash
dotnet test DRN.slnx
```

Tests use **Testcontainers** — Docker must be running for integration tests.

## Code Conventions

| Area | Convention |
|------|-----------|
| **DI** | Attribute-based: `[Scoped<T>]`, `[Singleton<T>]`, `[Transient<T>]` |
| **Entities** | Source-Known ID pattern; `[EntityType(byte)]` required |
| **DTOs** | Derive from `Dto`; live in `*.Contract` projects |
| **Testing** | DTT — integration-first with `[DataInline]` + `DrnTestContext` |
| **Git** | GitFlow-inspired: `develop` → `master` → tag `v*.*.*` |

## Architecture

DRN-Project follows **Domain-Driven Design (DDD)** with a layered architecture:

```
Domain → Infrastructure/Application → Hosted
```

See the per-package `README.md` files for detailed API documentation.

## License

By contributing, you agree that your contributions will be licensed under the same
license as the project (see [LICENSE](LICENSE)).

## Questions?

Open an issue or start a discussion on GitHub.
