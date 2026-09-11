# TaskLens

A global-hotkey, keyboard-first PowerToys Run plugin for viewing and acting on plain-markdown task lists (`- [ ] **Priority** — description`) without opening the notes app they live in — live search-as-you-type, sorted by priority, mark done / change priority / jump to the source note straight from the results list.

A second, independent consumer sits on the same interface: a small CLI (`tasklens get/list/search`, JSON output) meant for scripting or agent use, so a task's current info can be read without parsing files by hand.

## Status

Early development. `Core` (data model, `MarkdownVaultDataSource`) and `Cli` are built and working. `Plugin` (PowerToys Run integration) not started yet. Not yet installable as a PowerToys plugin.

```
dotnet run --project Cli -- list --priority High
dotnet run --project Cli -- search triton
dotnet run --project Cli -- get <id>
```

Points at this vault's `Tasks.md` by default; override with `TASKLENS_TASKS_FILE` / `TASKLENS_VAULT_ROOT` / `TASKLENS_VAULT_NAME` env vars.

## Architecture

Three layers:

- **`Core/`** — data-source-agnostic domain model (`TaskItem`, `Priority`) and the `ITaskDataSource` interface every consumer depends on.
- **`DataSources/MarkdownVault/`** — the one concrete implementation today: parses task lines, watches the source file for external changes, writes changes back preserving the original format.
- **`Plugin/`** — the PowerToys Run integration (`Wox.Plugin.IPlugin`), talks only to `ITaskDataSource`.
- **`Cli/`** — a thin command-line consumer of the same interface, JSON output.

See `WORKLOG.md` for build progress.

## Requirements

- Windows with [PowerToys](https://learn.microsoft.com/en-us/windows/powertoys/) installed
- .NET SDK (for building)

## Building

```
dotnet build
dotnet run --project SelfCheck   # parser + dual-write self-check
```
