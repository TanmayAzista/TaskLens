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

// --- MarkdownVaultDataSource, against a throwaway temp vault ---

{
    var vaultRoot = Directory.CreateTempSubdirectory("tasklens-selfcheck-").FullName;
    try
    {
        var tasksPath = Path.Combine(vaultRoot, "Tasks.md");
        var sourcePath = Path.Combine(vaultRoot, "Demo Project.md");

        File.WriteAllLines(tasksPath, new[]
        {
            "## Demo",
            "- [ ] **Medium** — a demo task ([[Demo Project]])",
        });
        File.WriteAllLines(sourcePath, new[]
        {
            "# Demo Project",
            "- [ ] **Medium** — a demo task",
        });

        using var source = new MarkdownVaultDataSource(tasksPath, vaultRoot, "DemoVault");

        var tasks = source.GetTasks();
        Check("reads exactly one task", tasks.Count == 1);
        Check("project comes from the ## heading", tasks.Count == 1 && tasks[0].Project == "Demo");
        Check("source URI resolves via obsidian://open", tasks.Count == 1 &&
            tasks[0].SourceNoteUri == "obsidian://open?vault=DemoVault&file=Demo%20Project");

        if (tasks.Count == 1)
        {
            source.SetPriority(tasks[0].Id, Priority.High);
            var afterSetPriority = File.ReadAllLines(tasksPath);
            Check("priority updated in Tasks.md", afterSetPriority.Any(l => l.Contains("**High**") && l.Contains("a demo task")));
            var mirroredAfterSet = File.ReadAllLines(sourcePath);
            Check("priority mirrored to source note", mirroredAfterSet.Any(l => l.Contains("**High**") && l.Contains("a demo task")));

            var refreshed = source.GetTasks();
            Check("re-read reflects the new priority", refreshed.Count == 1 && refreshed[0].Priority == Priority.High);

            source.MarkDone(refreshed[0].Id);
            var afterDone = source.GetTasks();
            Check("MarkDone removes the task from Tasks.md", afterDone.Count == 0);
            var sourceAfterDone = File.ReadAllLines(sourcePath);
            Check("MarkDone removes the mirrored line from the source note", !sourceAfterDone.Any(l => l.Contains("a demo task")));
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
