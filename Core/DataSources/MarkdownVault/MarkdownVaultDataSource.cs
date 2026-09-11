using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TaskLens.Core.DataSources.MarkdownVault;

public sealed class MarkdownVaultDataSource : ITaskDataSource, IDisposable
{
    private readonly string _tasksFilePath;
    private readonly string _vaultRootPath;
    private readonly string _vaultName;
    private readonly VaultFileWatcher _watcher;

    // Id -> what GetTasks() last saw for it, so SetPriority/MarkDone can locate the
    // exact line to edit without re-deriving it from scratch.
    private Dictionary<string, TaskRecord> _lastSeen = new();

    public event EventHandler? Changed;
    public event EventHandler<DataSourceMessage>? Message;

    public MarkdownVaultDataSource(string tasksFilePath, string vaultRootPath, string vaultName)
    {
        _tasksFilePath = tasksFilePath;
        _vaultRootPath = vaultRootPath;
        _vaultName = vaultName;

        _watcher = new VaultFileWatcher(tasksFilePath);
        _watcher.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<TaskItem> GetTasks()
    {
        var items = new List<TaskItem>();
        var seen = new Dictionary<string, TaskRecord>();
        string? currentProject = null;

        foreach (var line in File.ReadAllLines(_tasksFilePath))
        {
            if (line.StartsWith("## "))
            {
                currentProject = line[3..].Trim();
                continue;
            }

            if (!line.StartsWith("- [ ]"))
                continue;

            if (!TaskLineParser.TryParse(line, out var parsed, out var warning))
            {
                Message?.Invoke(this, new DataSourceMessage
                {
                    Severity = DataSourceMessage.Level.Warning,
                    Text = warning!,
                });
                continue;
            }

            var project = currentProject ?? "(unfiled)";
            var id = ComputeId(_tasksFilePath, line);
            var sourceUri = parsed!.SourceLinkTarget is { } target ? BuildObsidianUri(target) : null;

            items.Add(new TaskItem(id, parsed.Title, parsed.Priority, project, sourceUri));
            seen[id] = new TaskRecord(line, project, parsed);
        }

        _lastSeen = seen;
        return items;
    }

    public void SetPriority(string taskId, Priority priority)
    {
        var record = FindRecord(taskId);
        var newLine = $"- [ ] **{priority}** — {record.Parsed.Title}";

        ReplaceLineInFile(_tasksFilePath, record.RawLine, newLine);
        MirrorToSourceNote(record, newTitle: record.Parsed.Title, priority: priority, delete: false);
    }

    public void MarkDone(string taskId)
    {
        var record = FindRecord(taskId);

        RemoveLineFromFile(_tasksFilePath, record.RawLine);
        MirrorToSourceNote(record, newTitle: null, priority: null, delete: true);
    }

    private TaskRecord FindRecord(string taskId)
    {
        if (_lastSeen.TryGetValue(taskId, out var record))
            return record;

        GetTasks();
        if (_lastSeen.TryGetValue(taskId, out record))
            return record;

        throw new ArgumentException($"unknown task id: {taskId}", nameof(taskId));
    }

    // ponytail: matches the mirrored line in the source note by content-word overlap,
    // not exact text -- confirmed against the real vault that Tasks.md entries are
    // routinely condensed summaries of the source note's fuller line, not verbatim
    // copies (citation-only differences were the wrong assumption). Requires a clear
    // winner (no tie at the top score) and a minimum overlap; anything less confident
    // degrades to "warn, leave the source note alone" rather than risk editing the
    // wrong line. Good enough at this vault's scale; a stable per-task marker id
    // would be exact, but that means rewriting every task line across the user's
    // actual notes, which is a bigger call than this fix warrants on its own.
    private void MirrorToSourceNote(TaskRecord record, string? newTitle, Priority? priority, bool delete)
    {
        if (record.Parsed.SourceLinkTarget is not { } target)
            return;

        var sourceFile = ResolveSourceFile(target);
        if (sourceFile is null)
            return;

        var ourTokens = Tokenize(TaskLineParser.StripTrailingCitation(record.Parsed.Title));
        var lines = File.ReadAllLines(sourceFile);

        var candidates = new List<(int Index, ParsedTaskLine Parsed, int Score)>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].StartsWith("- [ ]"))
                continue;

            if (!TaskLineParser.TryParse(lines[i], out var parsed, out _))
                continue;

            var candidateTokens = Tokenize(TaskLineParser.StripTrailingCitation(parsed!.Title));
            var score = ourTokens.Count(candidateTokens.Contains);
            candidates.Add((i, parsed, score));
        }

        var ranked = candidates.OrderByDescending(c => c.Score).ToList();
        var minOverlap = Math.Max(3, ourTokens.Count / 3);
        var confident = ranked.Count > 0
            && ranked[0].Score >= minOverlap
            && ranked.Count(c => c.Score == ranked[0].Score) == 1;

        if (!confident)
        {
            Message?.Invoke(this, new DataSourceMessage
            {
                Severity = DataSourceMessage.Level.Warning,
                Text = $"no confident match for the mirrored line in {sourceFile} for task: {record.Parsed.Title}",
            });
            return;
        }

        var best = ranked[0];
        var updated = new List<string>(lines);
        if (delete)
        {
            updated.RemoveAt(best.Index);
        }
        else
        {
            updated[best.Index] = $"- [ ] **{priority}** — {best.Parsed.Title}";
        }

        File.WriteAllLines(sourceFile, updated);
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "to", "of", "in", "on", "for", "is", "are",
        "it", "its", "this", "that", "be", "as", "at", "by", "with", "not", "if",
        "into", "from", "than", "then", "but", "so", "do", "does",
    };

    private static readonly Regex WordToken = new(@"[a-zA-Z0-9]+", RegexOptions.Compiled);

    private static HashSet<string> Tokenize(string text) =>
        WordToken.Matches(text)
            .Select(m => m.Value.ToLowerInvariant())
            .Where(t => t.Length > 1 && !StopWords.Contains(t))
            .ToHashSet();

    // ponytail: linear scan of the vault per call — fine at a few hundred notes,
    // add a name->path index if this ever shows up on a profile.
    private string? ResolveSourceFile(string linkTarget)
    {
        if (linkTarget.Contains('/'))
        {
            var direct = Path.Combine(_vaultRootPath, linkTarget.EndsWith(".md") ? linkTarget : linkTarget + ".md");
            return File.Exists(direct) ? direct : null;
        }

        return Directory
            .EnumerateFiles(_vaultRootPath, "*.md", SearchOption.AllDirectories)
            .FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), linkTarget, StringComparison.OrdinalIgnoreCase));
    }

    private string BuildObsidianUri(string linkTarget) =>
        $"obsidian://open?vault={Uri.EscapeDataString(_vaultName)}&file={Uri.EscapeDataString(linkTarget)}";

    // Stable across re-parses; changes if the line's text is hand-edited elsewhere.
    private static string ComputeId(string filePath, string line)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{filePath}|{line}"));
        return Convert.ToHexString(hash)[..16];
    }

    private static void ReplaceLineInFile(string path, string oldLine, string newLine)
    {
        var lines = File.ReadAllLines(path);
        var index = Array.IndexOf(lines, oldLine);
        if (index < 0)
            throw new InvalidOperationException($"line not found in {path}: {oldLine}");

        lines[index] = newLine;
        File.WriteAllLines(path, lines);
    }

    private static void RemoveLineFromFile(string path, string line)
    {
        var lines = File.ReadAllLines(path).ToList();
        var index = lines.IndexOf(line);
        if (index < 0)
            throw new InvalidOperationException($"line not found in {path}: {line}");

        lines.RemoveAt(index);
        File.WriteAllLines(path, lines);
    }

    public void Dispose() => _watcher.Dispose();

    private sealed record TaskRecord(string RawLine, string Project, ParsedTaskLine Parsed);
}
