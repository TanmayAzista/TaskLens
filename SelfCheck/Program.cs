// ponytail: assert-based self-check in place of a test framework -- run with
// `dotnet run --project SelfCheck`. Fails loudly (non-zero exit) if the parser
// or the dual-write logic in MarkdownVaultDataSource regresses.
using TaskLens.Core;
using TaskLens.Core.DataSources.MarkdownVault;

var failures = 0;

void Check(string name, bool condition)
{
    if (condition) return;
    failures++;
    Console.WriteLine($"FAIL: {name}");
}

// --- TaskLineParser ---

{
    var ok = TaskLineParser.TryParse(
        "- [ ] **High** — Explore AIS correlation and dark-ship detection logic ([[OC14]])",
        out var parsed, out var warning);

    Check("parses a well-formed line", ok);
    Check("priority == High", parsed?.Priority == Priority.High);
    Check("title captured", parsed?.Title == "Explore AIS correlation and dark-ship detection logic ([[OC14]])");
    Check("source link extracted", parsed?.SourceLinkTarget == "OC14");
    Check("no warning on success", warning is null);
}

{
    var ok = TaskLineParser.TryParse(
        "- [ ] Missing the priority marker entirely",
        out var parsed, out var warning);

    Check("rejects a malformed line", !ok);
    Check("malformed line produces no ParsedTaskLine", parsed is null);
    Check("malformed line produces a warning", warning is not null);
}

{
    var title = "GPU abstraction layer (includes priority scheduling) ([[OC14 - GPU Management]], idea: [[Ideas/Triton|Triton]])";
    var stripped = TaskLineParser.StripTrailingCitation(title);
    Check("strips only the trailing citation, keeps mid-sentence parens",
        stripped == "GPU abstraction layer (includes priority scheduling)");
}

{
    var ok = TaskLineParser.TryParse("- [ ] **Low** — no trailing link here", out var parsed, out _);
    Check("parses a line with no citation group", ok);
    Check("source link is null when there's no citation", parsed?.SourceLinkTarget is null);
}

{
    TaskLineParser.TryParse("- [ ] **Low** — pipe-aliased link ([[Real Note|Display Text]])", out var parsed, out _);
    Check("pipe-aliased link extracts the target, not the alias", parsed?.SourceLinkTarget == "Real Note");
}

{
    TaskLineParser.TryParse("- [ ] **Low** — multiple links ([[First]], [[Second]])", out var parsed, out _);
    Check("first link in the citation wins when there are several", parsed?.SourceLinkTarget == "First");
}

{
    var ok = TaskLineParser.TryParse("- [ ] **high** — lowercase priority marker", out var parsed, out var warning);
    Check("rejects a lowercase priority marker rather than guessing", !ok);
    Check("lowercase-priority line produces a warning", warning is not null);
}

// --- TaskSearch ---

{
    var task = new TaskItem("id", "widget catalog sync", Priority.Medium, "Demo", null);
    Check("empty search term matches everything", TaskSearch.Matches(task, ""));
    Check("matches on a project-name token", TaskSearch.Matches(task, "demo"));
    Check("matches on a priority-name token", TaskSearch.Matches(task, "medium"));
    Check("multi-word term requires every token to match", TaskSearch.Matches(task, "widget sync"));
    Check("doesn't match an unrelated term", !TaskSearch.Matches(task, "triton"));
}

// --- MarkdownVaultDataSource, against a throwaway temp vault ---

{
    var vaultRoot = Directory.CreateTempSubdirectory("tasklens-selfcheck-").FullName;
    try
    {
        var tasksPath = Path.Combine(vaultRoot, "Tasks.md");
        var sourcePath = Path.Combine(vaultRoot, "Demo Project.md");

        // The source note's line is deliberately NOT a verbatim copy of Tasks.md's --
        // a condensed summary vs. the fuller original, same pattern confirmed against
        // the real vault (regression case for the exact-match bug the token-overlap
        // rewrite fixes). A second, unrelated task line is present so a confident
        // match has to actually pick the right one, not just the only one.
        File.WriteAllLines(tasksPath, new[]
        {
            "## Demo",
            "- [ ] **Medium** — widget catalog sync drops entries on retry ([[Demo Project]])",
            "- [ ] **Low** — unrelated task with no mirror anywhere ([[Demo Project]])",
            "- [x] **High** — already checked off, must not show up as a pending task",
        });
        File.WriteAllLines(sourcePath, new[]
        {
            "# Demo Project",
            "- [ ] **Medium** — investigate why the widget catalog sync job drops entries whenever a retry happens mid-batch",
        });

        using var source = new MarkdownVaultDataSource(tasksPath, vaultRoot, "DemoVault");

        var tasks = source.GetTasks();
        Check("reads both pending tasks and ignores the already-checked line", tasks.Count == 2);
        Check("project comes from the ## heading", tasks.All(t => t.Project == "Demo"));
        Check("source URI resolves via obsidian://open", tasks.Count == 2 &&
            tasks[0].SourceNoteUri == "obsidian://open?vault=DemoVault&file=Demo%20Project");

        var syncTask = tasks.FirstOrDefault(t => t.Title.Contains("widget catalog sync"));
        var unrelatedTask = tasks.FirstOrDefault(t => t.Title.Contains("unrelated task"));
        Check("found the condensed task", syncTask is not null);
        Check("found the unrelated task", unrelatedTask is not null);

        if (syncTask is not null && unrelatedTask is not null)
        {
            source.SetPriority(syncTask.Id, Priority.High);
            var afterSetPriority = File.ReadAllLines(tasksPath);
            Check("priority updated in Tasks.md", afterSetPriority.Any(l => l.Contains("**High**") && l.Contains("widget catalog sync")));
            var mirroredAfterSet = File.ReadAllLines(sourcePath);
            Check("condensed/fuller wording still finds and updates the right mirror line",
                mirroredAfterSet.Any(l => l.Contains("**High**") && l.Contains("drops entries whenever a retry")));

            // The unrelated task has no mirror in the source note at all -- must warn
            // and leave Tasks.md's own edit intact, not guess and clobber a wrong line.
            DataSourceMessage? warning = null;
            source.Message += (_, m) => warning = m;
            source.SetPriority(unrelatedTask.Id, Priority.High);
            Check("no-match case still updates Tasks.md", File.ReadAllLines(tasksPath).Any(l => l.Contains("**High**") && l.Contains("unrelated task")));
            Check("no-match case emits a warning instead of guessing", warning is { Severity: DataSourceMessage.Level.Warning });
            Check("source note is untouched by the no-match case", File.ReadAllLines(sourcePath).Length == 2);

            var refreshed = source.GetTasks();
            var refreshedSync = refreshed.First(t => t.Title.Contains("widget catalog sync"));
            Check("re-read reflects the new priority", refreshedSync.Priority == Priority.High);

            source.MarkDone(refreshedSync.Id);
            var afterDone = source.GetTasks();
            Check("MarkDone removes only the done task from Tasks.md", afterDone.Count == 1 && afterDone[0].Title.Contains("unrelated task"));
            var sourceAfterDone = File.ReadAllLines(sourcePath);
            Check("MarkDone removes the mirrored line from the source note", !sourceAfterDone.Any(l => l.Contains("widget catalog")));
        }
    }
    finally
    {
        Directory.Delete(vaultRoot, recursive: true);
    }
}

if (failures > 0)
{
    Console.WriteLine($"{failures} check(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine("All self-checks passed.");
