using System.Text.RegularExpressions;

namespace TaskLens.Core.DataSources.MarkdownVault;

// A line successfully parsed as a task, before Id/Project are attached by the caller.
public sealed record ParsedTaskLine(Priority Priority, string Title, string? SourceLinkTarget);

public static partial class TaskLineParser
{
    // "- [ ] **Priority** — description text ... (optionally trailing ([[links]]))"
    [GeneratedRegex(@"^- \[ \] \*\*(High|Medium|Low)\*\* — (.+)$")]
    private static partial Regex LinePattern();

    [GeneratedRegex(@"\[\[([^\]|]+)(?:\|[^\]]+)?\]\]")]
    private static partial Regex WikiLinkPattern();

    public static bool TryParse(string line, out ParsedTaskLine? parsed, out string? warning)
    {
        parsed = null;
        warning = null;

        var match = LinePattern().Match(line);
        if (!match.Success)
        {
            warning = $"couldn't parse task line: {line}";
            return false;
        }

        var priority = Enum.Parse<Priority>(match.Groups[1].Value);
        var title = match.Groups[2].Value.Trim();
        var sourceLinkTarget = ExtractTrailingCitationLink(title);

        parsed = new ParsedTaskLine(priority, title, sourceLinkTarget);
        return true;
    }

    // The task's source note is the first [[wikilink]] inside the line's trailing
    // "(...)" citation group, e.g. "... ([[OC14 - GPU Management]], idea: [[...]])".
    // Returns null if the line has no such trailing group, or it contains no link.
    private static string? ExtractTrailingCitationLink(string title)
    {
        var trimmed = title.TrimEnd();
        var range = FindTrailingParenRange(trimmed);
        if (range is not (int open, int end))
            return null;

        var citation = trimmed[(open + 1)..(end - 1)];
        var linkMatch = WikiLinkPattern().Match(citation);
        return linkMatch.Success ? linkMatch.Groups[1].Value.Trim() : null;
    }

    // Same title with its trailing "(...)" citation group (if any) removed, used to
    // match a Tasks.md line against its mirrored line in the source note, since the
    // two files' citation links legitimately differ (self-link vs. no self-link).
    public static string StripTrailingCitation(string title)
    {
        var trimmed = title.TrimEnd();
        var range = FindTrailingParenRange(trimmed);
        return range is (int open, int _) ? trimmed[..open].TrimEnd() : trimmed;
    }

    private static (int Open, int End)? FindTrailingParenRange(string text)
    {
        if (!text.EndsWith(')'))
            return null;

        var depth = 0;
        for (var i = text.Length - 1; i >= 0; i--)
        {
            if (text[i] == ')') depth++;
            else if (text[i] == '(')
            {
                depth--;
                if (depth == 0)
                    return (i, text.Length);
            }
        }

        return null;
    }
}
