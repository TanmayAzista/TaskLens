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
        ActionHandlers.BuildContextMenu(selectedResult, _dataSource!, Requery);

    private void Requery()
    {
        if (_context is not null && _lastQuery is not null)
        {
            _context.API.ChangeQuery(_lastQuery.RawQuery, true);
        }
    }

    public void Dispose() => _dataSource?.Dispose();
}
