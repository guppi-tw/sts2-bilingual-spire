#if DEBUG
using Godot;
using BilingualSpire.Localization;

namespace BilingualSpire.Development;

internal static class ReadabilityAudit
{
    internal static Dictionary<Control, int> Capture(Node root) => Descendants(root).OfType<Control>()
        .Where(c => c is Label or RichTextLabel && !string.IsNullOrWhiteSpace(LiveText.GetText(c)))
        .ToDictionary(c => c, FontSize);

    internal static void Report(string screen, Dictionary<Control, int> before)
    {
        foreach (var (label, previousSize) in before.OrderBy(pair => FontSize(pair.Key)))
        {
            if (!GodotObject.IsInstanceValid(label) || !label.IsVisibleInTree()) continue;
            var size = FontSize(label);
            if (size >= 18 && size >= previousSize * 0.7) continue;
            var text = LiveText.GetText(label)!.Replace('\n', ' ');
            ModEntry.Logger.Info($"READABILITY AUDIT {screen}/{label.GetPath()}: {previousSize} -> {size}, box={label.Size}, text={text[..Math.Min(100, text.Length)]}");
        }
    }

    private static int FontSize(Control control) => control.GetThemeFontSize(control is RichTextLabel ? "normal_font_size" : "font_size");

    private static IEnumerable<Node> Descendants(Node node)
    {
        yield return node;
        foreach (var child in node.GetChildren()) foreach (var descendant in Descendants(child)) yield return descendant;
    }
}
#endif
