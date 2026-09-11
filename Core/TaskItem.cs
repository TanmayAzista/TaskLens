namespace TaskLens.Core;

public record TaskItem(
    string Id,
    string Title,
    Priority Priority,
    string Project,
    string? SourceNoteUri
);
