# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `AgentQLPlugin.ExecuteQueryWithDescription(sqlQuery, resultDescription)` — a
  variant of `ExecuteQuery` where the model also supplies a short description of
  the result set (column meanings, filters, caveats) in the same tool call. Hosts
  that hand the raw rows to an orchestrator can skip the final prose-generation
  turn entirely, cutting one model round-trip per question.
- `AgentQLPlugin.LastSuccessfulResult` — per-scope capture of the last successful
  query (`CapturedQueryResult`: SQL, rows, row count, optional description), so
  hosts no longer need to wrap `ExecuteQuery` to harvest verified rows for charts
  or structured replies. Failed queries never overwrite a prior capture; a
  zero-row success is captured (it is an answer, not a failure).
- `SelfCorrectingChatClient` recognises `ExecuteQueryWithDescription` as a
  grounding query (new `ExecuteQueryWithDescriptionToolName` constant), so the
  self-correction guard works identically with either execute variant.

### Changed

- `GetDatabaseSchema` workflow text and the self-correction reminder now refer to
  "the query execution tool" generically instead of naming `ExecuteQuery`, since
  hosts may expose either execute variant.

## [0.2.1] — 2026-06-08

### Fixed

- ReadOnly statement validator wrongly rejected `WITH` (CTE) queries whose CTE
  name was double-quoted (e.g. `WITH "Summary" AS (...) SELECT ...`) as "malformed",
  silently neutralising a valid read-only query into empty results. The identifier
  reader now handles double-quoted identifiers (and digits in unquoted names), so
  quoted CTEs execute normally. The recursive write-detection guard still rejects a
  writable body inside a quoted CTE.

## [0.2.0] — 2026-06-08

### Added

- Self-correction guard for the query agent (`SelfCorrectingChatClient` +
  `UseAgentQLSelfCorrection`). It wraps the function-invocation loop and re-prompts
  the model with a system reminder whenever a turn ends without an answer or an
  explicit `ReportFailure` — restating the question and telling the model to read
  the query error, fix the SQL, and retry. After `MaxAttempts` nudges it returns a
  configurable exhaustion message, so the agent never returns an empty answer.
  Enabled by default in `AddAgentQLChat`; tune it via the new `configureSelfCorrection`
  parameter.
- .NET test infrastructure (unit + integration) with the project restructured into `src/`.
- Integration tests isolated with Respawn.

## [0.1.3] — 2026-03-10

### Fixed

- Increased the NuGet package icon resolution to 512x512.

## [0.1.2] — 2026-02-28

### Changed

- Improved the NuGet package icon with a centered flat design.

## [0.1.1] — 2026-02-16

### Fixed

- Corrected the NuGet repository URL, website, and authors metadata.

## [0.1.0] — 2026-02-11

### Added

- Initial release: EF Core schema introspection, safe SQL query execution,
  and the `Microsoft.Extensions.AI` plugin bridge (OpenAI, Anthropic, Ollama).
- Blazor demo app, README with usage guide, and NuGet packaging + publish workflow.

[Unreleased]: https://github.com/daniel3303/AgentQL/compare/v0.2.1...HEAD
[0.2.1]: https://github.com/daniel3303/AgentQL/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/daniel3303/AgentQL/compare/v0.1.3...v0.2.0
[0.1.3]: https://github.com/daniel3303/AgentQL/compare/v0.1.2...v0.1.3
[0.1.2]: https://github.com/daniel3303/AgentQL/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/daniel3303/AgentQL/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/daniel3303/AgentQL/releases/tag/v0.1.0
