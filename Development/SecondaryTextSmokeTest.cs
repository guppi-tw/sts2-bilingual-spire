#if DEBUG
using System.Text.RegularExpressions;
using Godot;

namespace BilingualSpire.Development;

internal static class SecondaryTextSmokeTest
{
    private static readonly Regex SizeTag = new(@"\[font_size=(\d+)\]", RegexOptions.Compiled);

    internal static string WithoutSize(string text) => SizeTag.Replace(text, "").Replace("[/font_size]", "");

    internal static void Check(RichTextLabel label)
    {
        var sizes = SizeTag.Matches(label.Text).Select(m => int.Parse(m.Groups[1].Value)).ToArray();
        if (sizes.Length == 0) throw new InvalidOperationException($"{label.GetPath()}: missing secondary text size");
        var primary = label.GetThemeFontSize("normal_font_size");
        if (sizes.Any(size => size >= primary || Math.Abs(size - primary * 0.85) > 0.51))
            throw new InvalidOperationException($"{label.GetPath()}: translation sizes [{string.Join(',', sizes)}] should be 85% of {primary}");
    }
}
#endif
