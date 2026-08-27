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

Wait for maintainer discussion and approval on the issue before beginning work.

### Submitting Changes

1. **Get issue approval first** — before writing code or opening a pull request, ensure there is an open issue describing your proposed change and that a maintainer has explicitly approved it. This ensures alignment with the project roadmap and prevents wasted effort.
2. **Fork** the repository
3. **Create a branch** from `develop`:
   ```bash
   git checkout -b feature/your-feature develop
   ```
4. **Make your changes** following the conventions below
5. **Write or update tests** — unit and analyzer tests before integration tests (DTT philosophy)
6. **Run the test suite**:

   ```bash
   dotnet run --project DRN.Test.Unit/DRN.Test.Unit.csproj
   dotnet run --project DRN.Test.Analyzer/DRN.Test.Analyzer.csproj
   dotnet run --project DRN.Test.Integration/DRN.Test.Integration.csproj
   ```
7. **Commit** with a clear message following [Conventional Commits](https://www.conventionalcommits.org/):
   ```
   feat(SharedKernel): add new entity base class
   fix(Utils): correct SKID timestamp overflow handling
   ```
8. **Push** your branch and open a **Pull Request** against `develop`

### Pull Request Guidelines

- Link and reference the approved issue (e.g., `Fixes #123` or `Closes #123`)
- Do not open pull requests without prior issue discussion and maintainer approval
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

### Dependency lock files

Commit `packages.lock.json` and `package-lock.json` whenever dependencies change. Supporting IDEs (e.g., Rider) update NuGet lock files automatically on restore.

To refresh NuGet lock files from the CLI:

```bash
dotnet restore DRN.slnx
```

CI restores in locked mode and rejects missing or stale lock files. Frontend dependency changes in `Sample.Hosted` (or any frontend project with a `package.json`) must update `package-lock.json` from within that project directory:

```bash
cd Sample.Hosted
npm install --package-lock-only --ignore-scripts
```

### Testing

```bash
dotnet run --project DRN.Test.Unit/DRN.Test.Unit.csproj
dotnet run --project DRN.Test.Analyzer/DRN.Test.Analyzer.csproj
dotnet run --project DRN.Test.Integration/DRN.Test.Integration.csproj
```

Tests use **Testcontainers** — Docker must be running for integration tests.

## Code Conventions

| Area | Convention |
|------|-----------|
| **DI** | Attribute-based: `[Scoped<T>]`, `[Singleton<T>]`, `[Transient<T>]` |
| **Entities** | Source-Known ID pattern; `[EntityType<TApp>(byte)]` or derived attribute required |
| **DTOs** | Derive from `Dto`; live in `*.Contract` projects |
| **Testing** | DTT — unit and analyzer tests before integration; `[Fact]` for no-data tests, data attributes request context only when needed |
| **Git** | GitFlow-inspired: `develop` → `master` → tag `v*.*.*` |

### Testing Attribute Examples

```csharp
[Fact]
public void Trim_Should_Remove_Outer_Whitespace()
{
    "  Duran  ".Trim().Should().Be("Duran");
}

[Theory]
[DataInline(AppEnvironment.Development, true)]
public void Feature_Should_Follow_Environment(DrnTestContext context,
    AppEnvironment environment, bool expected)
{
    context.AddToConfiguration(new { Environment = environment.ToString() });
    (environment == AppEnvironment.Development).Should().Be(expected);
}

[Theory]
[DataInlineUnit(2, 3, 5)]
public void Add_Should_Return_Correct_Sum(int a, int b, int expected)
{
    (a + b).Should().Be(expected);
}

[Theory]
[DataInlineUnit("SafeSection", "Visible", "safe-value")]
public void Unit_Configuration_Should_Be_Available(
    DrnTestContextUnit context, string section, string key, string value)
{
    context.AddToConfiguration(section, key, value);
    var debugView = context.GetConfigurationDebugView();

    debugView.SettingsByProvider.Values.SelectMany(settings => settings)
        .Should().Contain($"{section}:{key}={value}");
}
```

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
