# Worklog

Terse, one entry per meaningful step. Newest at the bottom.

- 2026-09-11 — Repo created, empty scaffold (README, .gitignore). No code yet.
- 2026-09-11 — .NET 10 SDK installed. Solution scaffolded (Core, SelfCheck).
- 2026-09-11 — `Core`: `TaskItem`/`Priority`/`ITaskDataSource`, `TaskLineParser` (regex + trailing-citation link extraction), `VaultFileWatcher` (debounced), `MarkdownVaultDataSource` (read + dual-write SetPriority/MarkDone, mirrors to source note by citation-stripped text match). Assert-based self-check (`SelfCheck/`) passes: parse success/failure paths, citation stripping, full read/SetPriority/MarkDone round-trip against a throwaway temp vault.
- 2026-09-11 — Resolved two open design decisions: `TaskItem.Id` = SHA256(tasksFilePath + raw line)[..16]; `SourceNoteUri` uses `obsidian://open?vault=Obsidian&file=...` (confirmed `obsidian://` is registered on this machine, vault name is the folder basename).
- 2026-09-11 — `Cli`: `tasklens get/list/search`, JSON to stdout, warnings to stderr. Read-only by design (per the doc's deliberate deferral of mutation-from-a-script). Smoke-tested against the real `Tasks.md` (34 tasks, 7 High-priority, zero parse warnings) — `list --priority`, `list --project`, `search`, `get` all confirmed working. Config via `TASKLENS_TASKS_FILE`/`TASKLENS_VAULT_ROOT`/`TASKLENS_VAULT_NAME` env vars, defaulting to this vault.
