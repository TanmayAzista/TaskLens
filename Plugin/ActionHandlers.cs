using System.Diagnostics;
using System.Windows.Input;
using TaskLens.Core;
using Wox.Plugin;

namespace TaskLens.Plugin;

public static class ActionHandlers
{
    // Enter: non-destructive default -- open the source note in Obsidian.
    // ponytail: obsidian://open jumps to the note itself, not a specific line --
    // the URI scheme only targets headings/blocks, which task lines aren't. Good
    // enough for now; a block-ref convention would be needed for exact-line jump.
    public static Func<ActionContext, bool> BuildEnterAction(TaskItem task) => _ =>
    {
        if (task.SourceNoteUri is { } uri)
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }

        return true;
    };

    public static List<ContextMenuResult> BuildContextMenu(Result result, ITaskDataSource dataSource, Action requery)
    {
        if (result.ContextData is not TaskItem task)
            return [];

        return
        [
            new ContextMenuResult
            {
                PluginName = "TaskLens",
                Title = "Mark done (Ctrl+D)",
                AcceleratorKey = Key.D,
                AcceleratorModifiers = ModifierKeys.Control,
                Action = _ =>
                {
                    dataSource.MarkDone(task.Id);
                    requery();
                    return false;
                },
            },
            new ContextMenuResult
            {
                PluginName = "TaskLens",
                Title = "Cycle priority (Ctrl+P)",
                AcceleratorKey = Key.P,
                AcceleratorModifiers = ModifierKeys.Control,
                Action = _ =>
                {
                    dataSource.SetPriority(task.Id, NextPriority(task.Priority));
                    requery();
                    return false;
                },
            },
        ];
    }

    private static Priority NextPriority(Priority priority) => priority switch
    {
        Priority.High => Priority.Medium,
        Priority.Medium => Priority.Low,
        Priority.Low => Priority.High,
        _ => priority,
    };
}
