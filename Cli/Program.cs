using System.Text.Json;
using System.Text.Json.Serialization;
using TaskLens.Core;
using TaskLens.Core.DataSources.MarkdownVault;

// Read-only by design for now (get/list/search) -- mutating a task from a script
// changes the trust model and is a deliberate later decision, not assumed here.

var tasksFile = Environment.GetEnvironmentVariable("TASKLENS_TASKS_FILE") ?? @"D:\2092_TanmayVerma\DOCS\Obsidian\Tasks.md";
var vaultRoot = Environment.GetEnvironmentVariable("TASKLENS_VAULT_ROOT") ?? @"D:\2092_TanmayVerma\DOCS\Obsidian";
var vaultName = Environment.GetEnvironmentVariable("TASKLENS_VAULT_NAME") ?? "Obsidian";

var jsonOptions = new JsonSerializerOptions
{
    Converters = { new JsonStringEnumConverter() },
};

using var source = new MarkdownVaultDataSource(tasksFile, vaultRoot, vaultName);
source.Message += (_, m) => Console.Error.WriteLine($"[{m.Severity}] {m.Text}");

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

return args[0] switch
{
    "get" => CmdGet(),
    "list" => CmdList(),
    "search" => CmdSearch(),
    _ => Unknown(),
};

int CmdGet()
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("usage: tasklens get <id>");
        return 1;
    }

    var task = source.GetTasks().FirstOrDefault(t => t.Id == args[1]);
    if (task is null)
    {
        Console.Error.WriteLine($"no task with id {args[1]}");
        return 1;
    }

    Console.WriteLine(JsonSerializer.Serialize(task, jsonOptions));
    return 0;
}

int CmdList()
{
    string? project = null;
    Priority? priority = null;

    for (var i = 1; i < args.Length; i++)
    {
        if (args[i] == "--project" && i + 1 < args.Length)
        {
            project = args[++i];
        }
        else if (args[i] == "--priority" && i + 1 < args.Length && Enum.TryParse<Priority>(args[i + 1], ignoreCase: true, out var p))
        {
            priority = p;
            i++;
        }
    }

    var tasks = source.GetTasks()
        .Where(t => project is null || string.Equals(t.Project, project, StringComparison.OrdinalIgnoreCase))
        .Where(t => priority is null || t.Priority == priority)
        .ToList();

    Console.WriteLine(JsonSerializer.Serialize(tasks, jsonOptions));
    return 0;
}

int CmdSearch()
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("usage: tasklens search <term>");
        return 1;
    }

    var term = string.Join(' ', args.Skip(1));
    var tasks = source.GetTasks().Where(t => TaskSearch.Matches(t, term)).ToList();
    Console.WriteLine(JsonSerializer.Serialize(tasks, jsonOptions));
    return 0;
}

int Unknown()
{
    PrintUsage();
    return 1;
}

void PrintUsage()
{
    Console.Error.WriteLine("usage: tasklens get <id> | tasklens list [--project X] [--priority High] | tasklens search <term>");
}
