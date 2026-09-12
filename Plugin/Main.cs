using TaskLens.Core;
using TaskLens.Core.DataSources.MarkdownVault;
using Wox.Plugin;

namespace TaskLens.Plugin;

// ponytail: vault paths hardcoded rather than a settings page (ISettingProvider) --
// this vault is the only consumer today; add a settings UI if TaskLens ever needs
// to point at a different one.
public sealed class Main : IPlugin, IContextMenu, IDisposable
{
    private const string TasksFilePath = @"D:\2092_TanmayVerma\DOCS\Obsidian\Tasks.md";
    private const string VaultRootPath = @"D:\2092_TanmayVerma\DOCS\Obsidian";
    private const string VaultName = "Obsidian";

    // Required by PowerToys Run's loader (not part of IPlugin itself -- it's a
    // reflection-based convention checked in PluginPair.cs before Init is ever
    // called) and must match plugin.json's "ID" exactly.
    public static string PluginID => "D3A714B7ED174C02972D0AC00FFD9DF0";

    // ponytail: found while investigating a real incident (see WORKLOG) -- 8
    // deliberate Ctrl+D presses each correctly marked-done whatever was selected
    // at that instant, but silently, so the user couldn't tell each press had
    // already landed on a different (reshuffled) task. This cooldown doesn't fix
    // that UX gap on its own (ShowNotification below does) -- it's separate
    // defense against PowerToys Run's own Launcher_KeyDown having no e.IsRepeat
    // guard, confirmed in its source: a genuinely *held* key would otherwise
    // cascade many rapid mutations from one physical keypress.
    private static readonly TimeSpan MutationCooldown = TimeSpan.FromMilliseconds(500);
    private DateTime _lastMutationAt = DateTime.MinValue;

    private PluginInitContext? _context;
    private MarkdownVaultDataSource? _dataSource;
    private Query? _lastQuery;

    public string Name => "TaskLens";

    public string Description => "View and act on vault tasks without opening Obsidian";

    public void Init(PluginInitContext context)
    {
        _context = context;
        _dataSource = new MarkdownVaultDataSource(TasksFilePath, VaultRootPath, VaultName);
    }

    public List<Result> Query(Query query)
    {
        _lastQuery = query;

        return _dataSource!.GetTasks()
            .Where(t => TaskSearch.Matches(t, query.Search))
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.Project, StringComparer.OrdinalIgnoreCase)
            .Select(task =>
            {
                var result = ResultBuilder.Build(task);
                result.Action = ActionHandlers.BuildEnterAction(task);
                return result;
            })
            .ToList();
    }

    public List<ContextMenuResult> LoadContextMenus(Result selectedResult) =>
        ActionHandlers.BuildContextMenu(selectedResult, _dataSource!, TryBeginMutation, Requery, Notify);

    private bool TryBeginMutation()
    {
        var now = DateTime.UtcNow;
        if (now - _lastMutationAt < MutationCooldown)
        {
            return false;
        }

        _lastMutationAt = now;
        return true;
    }

    private void Notify(string message) => _context?.API.ShowNotification("TaskLens", message);

    private void Requery()
    {
        if (_context is not null && _lastQuery is not null)
        {
            _context.API.ChangeQuery(_lastQuery.RawQuery, true);
        }
    }

    public void Dispose() => _dataSource?.Dispose();
}
