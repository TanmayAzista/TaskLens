using TaskLens.Core;
using Wox.Plugin;

namespace TaskLens.Plugin;

public static class ResultBuilder
{
    public static Result Build(TaskItem task) => new()
    {
        Title = task.Title,
        SubTitle = $"{task.Priority} — {task.Project}",
        IcoPath = "Images\\tasklens.light.png",
        Score = PriorityScore(task.Priority),
        ContextData = task,
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
