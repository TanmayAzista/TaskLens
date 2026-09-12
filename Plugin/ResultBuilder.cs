using TaskLens.Core;
using Wox.Plugin;

namespace TaskLens.Plugin;

public static class ResultBuilder
{
    // ponytail: queryTextDisplay must be the exact text already in the search box.
    // PowerToys Run overwrites the search box with a Result's QueryTextDisplay on
    // every arrow-key navigation (PluginManager.cs defaults it to Title when unset,
    // MainWindow.xaml.cs's UpdateTextBoxToSelectedItem applies it unconditionally
    // when non-empty) -- leaving it unset replaced the user's typed search with each
    // task's long Title as they arrow-navigated. Setting it to the current query
    // makes that overwrite a no-op instead.
    public static Result Build(TaskItem task, string queryTextDisplay) => new()
    {
        Title = task.Title,
        SubTitle = $"{task.Priority} — {task.Project}",
        IcoPath = "Images\\tasklens.light.png",
        Score = PriorityScore(task.Priority),
        ContextData = task,
        QueryTextDisplay = queryTextDisplay,
    };

    // PowerToys Run orders results by descending Score -- this is what delivers
    // the design's "priority major" sort (project-minor comes from Main's OrderBy,
    // which Query() applies before Score-based re-ranking narrows on a search term).
    private static int PriorityScore(Priority priority) => priority switch
    {
        Priority.High => 300,
        Priority.Medium => 200,
        Priority.Low => 100,
        _ => 0,
    };
}
