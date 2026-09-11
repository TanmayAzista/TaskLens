namespace TaskLens.Core;

public static class TaskSearch
{
    // Token-based, case-insensitive substring match across title, project, and
    // priority -- shared by the CLI's `search` and (later) the PowerToys Run
    // plugin's live query, so both consumers fuzzy-match the same way.
    public static bool Matches(TaskItem task, string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return true;

        var haystack = $"{task.Title} {task.Project} {task.Priority}".ToLowerInvariant();
        var tokens = term.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.All(haystack.Contains);
    }
}
