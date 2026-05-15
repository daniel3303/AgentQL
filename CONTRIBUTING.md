# Contributing to AgentQL

Thanks for your interest in improving AgentQL.

## Getting set up

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/) and (for the
integration tests) Docker.

```bash
dotnet tool restore        # restores CSharpier (pinned in .config/dotnet-tools.json)
brew install prek          # or: pipx install pre-commit
prek install -f            # installs the git hooks
prek run --all-files       # run every hook once to confirm a clean baseline
```

## Build, format, test

```bash
dotnet build -c Release -warnaserror     # warnings are errors, as in CI
dotnet csharpier format .                # apply formatting
dotnet csharpier check .                 # verify formatting (CI gate)
dotnet test                              # unit + integration (Docker required)
```

Fast inner loop without Docker:

```bash
dotnet test --project tests/Equibles.AgentQL.UnitTests/Equibles.AgentQL.UnitTests.csproj
```

## Branching

Branch from `main` using a Conventional-Commit prefix:

`feat/`, `fix/`, `chore/`, `docs/`, `ci/`, `style/`, `refactor/`, `test/`.

## Commits and PR titles

This repo squash-merges, and the squash commit takes the **PR title** — so the
PR title must be a valid [Conventional Commit](https://www.conventionalcommits.org/)
(e.g. `feat: add Oracle schema detection`). A workflow enforces this.

Update `CHANGELOG.md` under `## [Unreleased]` for any user-visible change.

## Reporting bugs and requesting features

Open an issue using the templates. For security vulnerabilities, use
[private reporting](https://github.com/daniel3303/AgentQL/security/advisories/new)
instead of a public issue — see [SECURITY.md](SECURITY.md).

By contributing you agree to abide by the
[Code of Conduct](CODE_OF_CONDUCT.md).
