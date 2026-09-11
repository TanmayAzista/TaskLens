namespace TaskLens.Core;

public class DataSourceMessage
{
    public enum Level { Info, Warning, Error }

    public required Level Severity { get; init; }
    public required string Text { get; init; }
}

public interface ITaskDataSource
{
    IReadOnlyList<TaskItem> GetTasks();
    void SetPriority(string taskId, Priority priority);
    void MarkDone(string taskId);

    event EventHandler? Changed;
    event EventHandler<DataSourceMessage>? Message;
}
