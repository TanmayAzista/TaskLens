# Worklog

Terse, one entry per meaningful step. Newest at the bottom.

- 2026-09-11 — Repo created, empty scaffold (README, .gitignore). No code yet.
- 2026-09-11 — .NET 10 SDK installed. Solution scaffolded (Core, SelfCheck).
- 2026-09-11 — `Core`: `TaskItem`/`Priority`/`ITaskDataSource`, `TaskLineParser` (regex + trailing-citation link extraction), `VaultFileWatcher` (debounced), `MarkdownVaultDataSource` (read + dual-write SetPriority/MarkDone, mirrors to source note by citation-stripped text match). Assert-based self-check (`SelfCheck/`) passes: parse success/failure paths, citation stripping, full read/SetPriority/MarkDone round-trip against a throwaway temp vault.
- 2026-09-11 — Resolved two open design decisions: `TaskItem.Id` = SHA256(tasksFilePath + raw line)[..16]; `SourceNoteUri` uses `obsidian://open?vault=Obsidian&file=...` (confirmed `obsidian://` is registered on this machine, vault name is the folder basename).
