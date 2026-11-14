using System.Text.RegularExpressions;

namespace BilingualSpire.Localization;

internal static class ParagraphText
{
    private static readonly Regex Break = new(@"\n[\t ]*\n(?:[\t ]*\n)*", RegexOptions.Compiled);
    private static readonly Regex Tag = new(@"\[(?<close>/)?(?<name>[a-zA-Z_][a-zA-Z_0-9]*)(?:[ =][^\]\r\n]*)?\]", RegexOptions.Compiled);
    private static readonly HashSet<string> Standalone = new(StringComparer.OrdinalIgnoreCase) { "lb", "rb", "br", "hr" };
    private static readonly HashSet<string> Structural = new(StringComparer.OrdinalIgnoreCase) { "img", "table", "cell", "ul", "ol" };

    internal static string Combine(string first, string second, bool richText = true, int? secondaryFontSize = null)
    {
        if (first == second) return first;
        string Style(string text) => richText && secondaryFontSize is { } size
            ? $"[font_size={size}]{text}[/font_size]" : text;
        var a = Split(first, richText);
        var b = Split(second, richText);
        // Official translations sometimes merge or split paragraphs; don't invent a correspondence.
        if (a is null || b is null || a.Length < 2 || a.Length != b.Length) return first + "\n" + Style(second);
        return string.Join("\n\n", a.Zip(b, (original, translation) => original == translation
            ? original : original + "\n" + Style(translation)));
    }

    private static string[]? Split(string text, bool richText)
    {
        var parts = Break.Split(text.Replace("\r\n", "\n").Replace('\r', '\n'));
        if (!richText || parts.Length < 2) return parts;
        var active = new List<(string Name, string Opening)>();
        var result = new List<string>();
        foreach (var part in parts)
        {
            var prefix = string.Concat(active.Select(t => t.Opening));
            foreach (Match tag in Tag.Matches(part))
            {
                var name = tag.Groups["name"].Value;
                if (Standalone.Contains(name)) continue;
                if (tag.Groups["close"].Success)
                {
                    if (active.Count == 0 || !string.Equals(active[^1].Name, name, StringComparison.OrdinalIgnoreCase)) return null;
                    active.RemoveAt(active.Count - 1);
                }
                else active.Add((name, tag.Value));
            }
            // Tables, lists and images must remain a single BBCode structure.
            if (active.Any(t => Structural.Contains(t.Name))) return null;
            result.Add(prefix + part + string.Concat(active.AsEnumerable().Reverse().Select(t => $"[/{t.Name}]")));
        }
        return active.Count == 0 ? result.ToArray() : null;
    }
}
