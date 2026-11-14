#if DEBUG
using BilingualSpire.Localization;

namespace BilingualSpire.Development;

internal static class ParagraphSmokeTest
{
    internal static void Run()
    {
        Check("First paragraph.\n\nSecond paragraph.", "最初の段落。\n\n次の段落。",
            "First paragraph.\n最初の段落。\n\nSecond paragraph.\n次の段落。");
        Check("One line\ncontinued\n\nNext", "一行\n続き\n\n次", "One line\ncontinued\n一行\n続き\n\nNext\n次");
        Check("One\r\n \r\nTwo", "一\n\n二", "One\n一\n\nTwo\n二");
        Check("[center][b]One\n\nTwo[/b][/center]", "[center][b]一\n\n二[/b][/center]",
            "[center][b]One[/b][/center]\n[center][b]一[/b][/center]\n\n[center][b]Two[/b][/center]\n[center][b]二[/b][/center]");
        Check("[red]One[/red]\n\n[img]res://icon.png[/img] Two", "[red]一[/red]\n\n[img]res://icon.png[/img] 二",
            "[red]One[/red]\n[red]一[/red]\n\n[img]res://icon.png[/img] Two\n[img]res://icon.png[/img] 二");
        Check("Same\n\nTwo", "Same\n\n二", "Same\n\nTwo\n二");
        Check("One\n\nTwo", "一と二", "One\n\nTwo\n一と二");
        Check("[table=1][cell]A\n\nB[/cell][/table]", "[table=1][cell]あ\n\nい[/cell][/table]",
            "[table=1][cell]A\n\nB[/cell][/table]\n[table=1][cell]あ\n\nい[/cell][/table]");
        Check("[b]One\n\nTwo", "一\n\n二", "[b]One\n\nTwo\n一\n\n二");
        Check("Same\n\nSame", "Same\n\nSame", "Same\n\nSame");
        var styled = ParagraphText.Combine("[center]One\n\nTwo[/center]", "[center]一\n\n二[/center]", secondaryFontSize: 17);
        if (styled != "[center]One[/center]\n[font_size=17][center]一[/center][/font_size]\n\n[center]Two[/center]\n[font_size=17][center]二[/center][/font_size]")
            throw new InvalidOperationException("Secondary size must wrap each translated paragraph without affecting the primary text");
        ModEntry.Logger.Info("PARAGRAPH PASS: paragraph order, line breaks, formatting, duplicates and safe fallback");
    }

    private static void Check(string first, string second, string expected)
    {
        var actual = ParagraphText.Combine(first, second);
        if (actual != expected) throw new InvalidOperationException($"Paragraph formatting: expected {expected}, got {actual}");
    }
}
#endif
