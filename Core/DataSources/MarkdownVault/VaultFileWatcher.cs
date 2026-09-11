namespace TaskLens.Core.DataSources.MarkdownVault;

// Watches a single file and raises Changed at most once per debounce window,
// since editors and Claude Code both tend to write in bursts.
public sealed class VaultFileWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly System.Timers.Timer _debounceTimer;

    public event EventHandler? Changed;

    public VaultFileWatcher(string filePath, TimeSpan? debounce = null)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new ArgumentException($"file path has no directory: {filePath}", nameof(filePath));
        var fileName = Path.GetFileName(filePath);

        _debounceTimer = new System.Timers.Timer((debounce ?? TimeSpan.FromMilliseconds(300)).TotalMilliseconds)
        {
            AutoReset = false,
        };
        _debounceTimer.Elapsed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        _watcher.Changed += (_, _) => RestartDebounce();
        _watcher.EnableRaisingEvents = true;
    }

    private void RestartDebounce()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _debounceTimer.Dispose();
    }
}
