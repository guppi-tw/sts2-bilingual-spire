#if DEBUG
using BilingualSpire.Localization;
using BilingualSpire.Settings;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.addons.mega_text;

namespace BilingualSpire.Development;

internal static class MapLegendSmokeTest
{
    internal static async Task Run(NMapScreen map)
    {
        CheckLayout(map);
        await Screenshot("bilingual");
        for (var i = 0; i < 3; i++)
        {
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.F8, Keycode = Key.F8, Pressed = true });
            await WaitForKeyFrame();
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.F8, Keycode = Key.F8, Pressed = false });
            await WaitForKeyFrame();
            if (i < 2)
            {
                var legend = map.GetNode<Control>("%MapLegend");
                Require(!LanguageSwitcher.IsBilingual && legend.Size.Y == 454, "F8 restores native legend height");
                foreach (var row in legend.GetNode<Control>("LegendItems").GetChildren().OfType<NMapLegendItem>())
                {
                    Require(row.Size.Y == 48 && row.GetNode<MegaLabel>("MegaLabel").Visible, "F8 restores native rows and labels");
                    Require(!row.GetNode<RichTextLabel>("BilingualLegendText").Visible, "Single-language mode hides bilingual text");
                }
                await Screenshot(i == 0 ? "english" : "japanese");
            }
            else CheckLayout(map);
        }
        foreach (var language in LanguagePreferences.Languages)
        {
            var other = language == "jpn" ? "eng" : "jpn";
            foreach (var reverse in new[] { false, true })
            {
                LanguageSwitcher.SetPair(reverse ? other : language, reverse ? language : other);
                await Settle();
                CheckLayout(map);
            }
        }
        LanguageSwitcher.SetPair("fra", "kor");
        await Settle();
        CheckLayout(map);
        await Screenshot("french-korean");
        LanguageSwitcher.SetPair("eng", "jpn");
        await Settle();
        var first = map.GetNode<Control>("%MapLegend").GetNode<Control>("LegendItems").GetChild<NMapLegendItem>(0);
        first.Call("OnFocus");
        await Settle();
        Require(first.MouseFilter != Control.MouseFilterEnum.Ignore, "Legend retains native interaction");
        first.Call("OnUnfocus");
        var before = first.GetNode<RichTextLabel>("BilingualLegendText").Text;
        for (var i = 0; i < 3; i++) LanguageSwitcher.ShowBilingual();
        await Settle();
        Require(first.GetNode<RichTextLabel>("BilingualLegendText").Text == before, "Repeated updates preserve text and font size");
        CheckLayout(map);
        ModEntry.Logger.Info("MAP LEGEND PASS: six rows and header fit; F8 restores native layout; 16 languages in both positions, French/Korean, focus and repeated refresh");
    }

    private static void CheckLayout(NMapScreen map)
    {
        var legend = map.GetNode<Control>("%MapLegend");
        Require(((TextureRect)legend).StretchMode == TextureRect.StretchModeEnum.Scale, "Native parchment covers the expanded legend");
        var rows = legend.GetNode<Control>("LegendItems").GetChildren().OfType<NMapLegendItem>().ToArray();
        Require(rows.Length == 6, "All six map legend items are present");
        var boxes = new List<Rect2>();
        var header = legend.GetNode<RichTextLabel>("BilingualLegendText");
        CheckText(header, legend.GetGlobalRect());
        foreach (var row in rows)
        {
            var label = row.GetNode<RichTextLabel>("BilingualLegendText");
            Require(SecondaryTextSmokeTest.WithoutSize(label.Text) == row.GetNode<MegaLabel>("MegaLabel").Text,
                $"{row.Name}: translation '{label.Text}' differs from native '{row.GetNode<MegaLabel>("MegaLabel").Text}'");
            var paint = CheckText(label, row.GetGlobalRect());
            Require(legend.GetGlobalRect().Encloses(paint), $"{row.Name}: text fits parchment");
            Require(!row.GetNode<MegaLabel>("MegaLabel").Visible, "Original text is not drawn underneath");
            Require(boxes.All(box => !box.Intersects(paint)), "Adjacent legend entries do not overlap");
            boxes.Add(paint);
        }
    }

    private static Rect2 CheckText(RichTextLabel label, Rect2 bounds)
    {
        var paint = new Rect2(label.GlobalPosition, new Vector2(label.GetContentWidth(), label.GetContentHeight()));
        Require(label.IsVisibleInTree(), $"{label.GetPath()}: bilingual text is visible");
        Require(bounds.Grow(1).Encloses(paint), $"{label.GetPath()}: text={paint} exceeds bounds={bounds}");
        Require(label.GetThemeFontSize("normal_font_size") >= 16, "Legend remains readable");
        var font = label.GetThemeFont("normal_font");
        foreach (var rune in label.GetParsedText().EnumerateRunes().Where(System.Text.Rune.IsLetter))
        {
            if (font.HasChar(rune.Value)) continue;
            // HasChar excludes automatic system fallbacks; check the actual shaped glyph.
            using var line = new TextLine();
            line.AddString(rune.ToString(), font, label.GetThemeFontSize("normal_font_size"));
            line.GetSize();
            Require(TextServerManager.GetPrimaryInterface().ShapedTextGetGlyphs(line.GetRid())
                .Any(glyph => glyph["font_rid"].AsRid().IsValid), $"Legend can render {rune}");
        }
        if (label.Text.Contains("[font_size=")) SecondaryTextSmokeTest.Check(label);
        else Require(!label.Text.Contains('\n'), "Identical translations appear only once");
        return paint;
    }

    private static async Task Settle()
    {
        var tree = NGame.Instance!.GetTree();
        for (var i = 0; i < 3; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static async Task WaitForKeyFrame()
    {
        var tree = NGame.Instance!.GetTree();
        await tree.ToSignal(tree.CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
    }

    private static async Task Screenshot(string name)
    {
        if (System.Environment.GetEnvironmentVariable("BILINGUAL_SPIRE_SCREENSHOT") is not { } screenshot) return;
        RenderingServer.ForceDraw();
        await Task.CompletedTask;
        var file = Path.Combine(Path.GetDirectoryName(screenshot)!, $"map-legend-{name}.png");
        Require(NGame.Instance!.GetViewport().GetTexture().GetImage().SavePng(file) == Error.Ok, "Map screenshot saved");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
