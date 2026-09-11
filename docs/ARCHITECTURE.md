# Architecture

How TaskLens actually works, for anyone maintaining or extending it. For
install/quick-start, see the [README](../README.md). The original
pre-implementation design lives in Tanmay's vault (`TaskLens - Design
Plan.md`) — this doc is the as-built version, including what changed
during the build.

## Component model

`Core` compiles as a shared class library, not code private to either
consumer — that's what lets the PowerToys plugin and the CLI read the
exact same tasks through the exact same interface, with no second data
model to keep in sync:

```
                    ┌────────────────────────────┐
                    │           Core              │
                    │  TaskItem, Priority          │
                    │  ITaskDataSource (the port)  │
                    │  TaskSearch (shared filter)  │
                    │  DataSources/MarkdownVault/   │
                    │    TaskLineParser            │
                    │    MarkdownVaultDataSource    │
                    │    VaultFileWatcher          │
                    └──────────┬──────────┬────────┘
                               │          │
                    ┌──────────┘          └──────────┐
                    │                                 │
            ┌───────▼────────┐              ┌─────────▼────────┐
            │     Plugin      │              │        Cli         │
            │  Wox.Plugin.    │              │  tasklens get/     │
            │  IPlugin/       │              │  list/search,       │
            │  IContextMenu   │              │  JSON stdout         │
            │  (PowerToys Run)│              │  (scripts, agents)  │
            └─────────────────┘              └─────────────────────┘
```

Neither consumer knows anything about markdown, file paths, or dual-write
— they only see `ITaskDataSource`. Today there's exactly one
implementation (`MarkdownVaultDataSource`), but the split means a second
one (a different vault format, a database, whatever) would only require
implementing the interface again, not touching `Plugin` or `Cli`.

## Line format & parsing

Tasks live in one file, `Tasks.md`, as:

```
- [ ] **Priority** — description text, may include [[wikilinks]] and
  (parenthetical asides) ... (trailing citation: [[Source Note]], idea: [[...]])
```

`TaskLineParser.TryParse` matches the checkbox/priority/em-dash prefix
with one regex; anything after that (the whole rest of the line) becomes
`Title` verbatim — deliberately not stripped or reformatted, since a real
description can contain its own parenthetical asides that look
structurally identical to the trailing citation group. A line that
doesn't match the prefix produces a `Warning` message and is skipped,
not a crash — the rest of the file still loads.

The task's **source note link** is extracted separately: find the line's
*trailing* `(...)` group (balanced-paren scan from the end, so nested
parens inside the real citation don't break it), then take the first
`[[wikilink]]` inside that group. This is how `SourceNoteUri` gets
built — `obsidian://open?vault=<name>&file=<link target>` — confirmed
against this machine's actual registered `obsidian://` handler and vault
name (the vault folder's basename; no custom name override configured).

**Known limit**: `obsidian://open` opens the *note*, not a specific
*line* — the URI scheme only targets headings and block references,
which arbitrary task lines aren't. `Enter` in the plugin gets you to the
right note, not the right line within it.

## Dual write & the mirror-matching heuristic

`SetPriority`/`MarkDone` update the `Tasks.md` line directly (it's the
line we're already holding), then try to find and update the *same*
task's line in its linked source note — because `Tasks.md`'s own header
states the convention: it mirrors what's checked off in each project's
own notes, and a done task should disappear from both, not get left
checked-off in one.

The first implementation matched source-note lines by exact text
equality (citation stripped from both sides). Real-vault testing (not
the original self-check, which happened to use a synthetic pair that
matched verbatim) found this false: `Tasks.md` entries are routinely
*condensed summaries* of the fuller line in their source note, not
verbatim copies that only differ in citation. Exact-match would silently
fail to find the mirror line for a real, non-trivial fraction of tasks —
updating `Tasks.md` while quietly leaving the source note stale, which
breaks the whole point of dual-write without ever surfacing an error.

The fix: **content-word overlap**, not exact text. Both lines'
descriptions are tokenized (stopwords filtered), and the source-note
line with the most shared tokens wins — but only if that top score
clears a minimum threshold *and* has no tie with a second candidate.
Anything less confident warns and leaves the source note untouched
rather than risk silently editing the wrong line. A stable per-task
marker id (an HTML comment embedded in both copies at creation time)
would be exact instead of heuristic, but was deliberately not built:
it means rewriting every existing task line's markdown across Tanmay's
actual vault notes, which is a bigger, more consequential change to
his real notes than either build agent judged appropriate to make
unilaterally off a peer-coordinated build. Worth revisiting directly
with him if the heuristic ever proves insufficient in practice.

## Target framework split

`Core` multi-targets `net10.0;net9.0`. `Cli` and `SelfCheck` build and
run against `net10.0` (matches the .NET SDK/runtime actually installed
for local dev). `Plugin` is pinned to `net9.0-windows` — confirmed by
reflecting the installed Calculator Run-plugin's `deps.json`
(`.NETCoreApp,Version=v9.0`) rather than assumed, since PowerToys loads
plugin assemblies into its own already-running process and won't cross
major versions. Splitting the TFM (rather than forcing the whole
solution onto net9.0, which isn't installed as a runtime on this
machine) keeps `dotnet run --project Cli` working without an extra
runtime install, since the CLI never runs inside PowerToys' process.

## PowerToys Run integration specifics

- **`Wox.Plugin.dll` is referenced directly** (`HintPath`, not a NuGet
  package — none exists for it) **with `Private=false`.** PowerToys'
  own process already has this assembly loaded; shipping a second copy
  in the plugin's own folder would load as a distinct assembly identity
  and break type matching across the plugin boundary (a `Result` built
  against "our" `Wox.Plugin` wouldn't satisfy an API expecting the
  host's). Confirmed this matches the real, installed Calculator plugin,
  which doesn't ship its own copy either.
- **Sort order** comes from `Result.Score`, not application-side
  sorting logic — `ResultBuilder` encodes priority as a score
  (High=300/Medium=200/Low=100) and lets PowerToys Run's own
  descending-score ordering do the priority-major sort, since that's
  the mechanism the host already re-applies as a search term narrows
  results.
- **Ctrl+D (mark done) / Ctrl+P (cycle priority)** are `IContextMenu`
  entries, not `Result.Action` — `Result.Action` is the *default* (Enter)
  action only. Both context-menu actions return `false` (keep the
  results window open) and call `IPublicAPI.ChangeQuery` with the same
  query text to force a re-query against the now-changed data, rather
  than closing the window the way the default Enter action does.

## Testing

`SelfCheck/` is an assert-based console check, not a test framework —
`dotnet run --project SelfCheck`, exits non-zero if anything fails.
Deliberate: this is a small, single-maintainer tool where `dotnet test`
+ xUnit/NUnit would be a dependency and a fixture-lifecycle model for a
problem a plain `if (!condition) failures++` loop already solves.
Covers: parser success/failure paths, trailing-citation stripping
(including a mid-sentence paren that isn't the citation), and a full
`GetTasks`/`SetPriority`/`MarkDone` round-trip against a throwaway temp
vault — including a condensed-vs-fuller regression case modeled on the
real mirror-matching bug, and a no-confident-match case proving the
"warn and leave it alone" fallback actually leaves it alone.

No write operation (`SetPriority`/`MarkDone`) has been run against the
*real* vault by either build agent — that stays a throwaway-temp-vault-only
test by design; running it for real is Tanmay's call, not something
decided unilaterally mid-build.

## Known limits

- **`obsidian://open` can't target a line**, only the note (see Line
  format above).
- **Mirror matching is a confidence heuristic, not exact** (see Dual
  write above) — correct for every real case tested so far, but not
  provably complete.
- **`ResolveSourceFile` is a linear scan of the vault per call** (find a
  file by basename under the vault root). Fine at a few hundred notes;
  would want a name→path index if the vault grows enough for this to
  show up on a profile.
- **Vault paths are hardcoded constants** in `Plugin/Main.cs` and
  default env vars in `Cli/Program.cs`, not a settings page
  (`ISettingProvider` exists in `Wox.Plugin` but isn't used) — this
  vault is the only consumer today.
- **No auth/permission model** — anything that can run `dotnet run
  --project Cli` or load the plugin can read and (once mutation ships)
  write every task. Fine for a single-user dev box; not designed for
  anything beyond that.
