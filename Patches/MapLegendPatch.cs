using System.Runtime.CompilerServices;
using BilingualSpire.Localization;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.addons.mega_text;

namespace BilingualSpire.Patches;

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen._Ready))]
internal static class MapLegendPatch
{
    private sealed record TextSlot(MegaLabel Original, RichTextLabel Bilingual, bool Visible, int MaxFontSize);
    private sealed record Row(NMapLegendItem Item, Vector2 Minimum, TextSlot Text);
    private sealed record State(TextureRect Legend, TextureRect.StretchModeEnum StretchMode, Control Items, float Top, float Bottom,
        float ItemsTop, float ItemsBottom, TextSlot Header, Row[] Rows);
    private static readonly ConditionalWeakTable<NMapScreen, State> Screens = new();
    private static readonly string[] Keys = ["LEGEND_UNKNOWN", "LEGEND_MERCHANT", "LEGEND_TREASURE", "LEGEND_REST", "LEGEND_ENEMY", "LEGEND_ELITE"];

    private static void Postfix(NMapScreen __instance)
    {
        try
        {
            var legend = __instance.GetNode<TextureRect>("%MapLegend");
            var items = legend.GetNode<Control>("LegendItems");
            var rows = items.GetChildren().OfType<NMapLegendItem>().ToArray();
            if (rows.Length != Keys.Length) return;
            var state = new State(legend, legend.StretchMode, items, legend.OffsetTop, legend.OffsetBottom, items.OffsetTop, items.OffsetBottom,
                CreateText(legend.GetNode<MegaLabel>("Header"), "LEGEND_HEADER", 36),
                rows.Select((row, index) => new Row(row, row.CustomMinimumSize,
                    CreateText(row.GetNode<MegaLabel>("MegaLabel"), Keys[index] + ".title", 28))).ToArray());
            Screens.Add(__instance, state);
            Apply(state);
        }
        catch (Exception error) { LiveText.Report(error); }
    }

    private static TextSlot CreateText(MegaLabel original, string key, int maxFontSize)
    {
        // A native label can skip its Text setter when the scene already contains that
        // translation. Bind it explicitly so single-language mode also refreshes it.
        LiveText.BindRenderer(original, () => new LocString("map", key).GetFormattedText());
        var rich = new RichTextLabel
        {
            Name = "BilingualLegendText", BbcodeEnabled = true, ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.Off, MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        foreach (var name in new[] { "normal_font", "bold_font", "italics_font", "bold_italics_font" })
            rich.AddThemeFontOverride(name, original.GetThemeFont("font"));
        rich.AddThemeColorOverride("default_color", original.GetThemeColor("font_color"));
        rich.AddThemeColorOverride("font_shadow_color", original.GetThemeColor("font_shadow_color"));
        foreach (var name in new[] { "shadow_offset_x", "shadow_offset_y" })
            rich.AddThemeConstantOverride(name, original.GetThemeConstant(name));
        SetSize(rich, maxFontSize);
        original.GetParent().AddChild(rich);
        LiveText.BindRenderer(rich, () => original.HorizontalAlignment == HorizontalAlignment.Center
            ? $"[center]{new LocString("map", key).GetFormattedText()}[/center]"
            : new LocString("map", key).GetFormattedText());
        return new(original, rich, original.Visible, maxFontSize);
    }

    internal static void Refresh()
    {
        foreach (var (screen, state) in Screens.ToArray())
        {
            if (!GodotObject.IsInstanceValid(screen) || screen.IsQueuedForDeletion()) continue;
            try { Apply(state); }
            catch (Exception error) { LiveText.Report(error); }
        }
    }

    private static void Apply(State state)
    {
        var bilingual = LanguageSwitcher.IsBilingual;
        var center = (state.Top + state.Bottom) / 2;
        state.Legend.OffsetTop = bilingual ? center - 340 : state.Top;
        state.Legend.OffsetBottom = bilingual ? center + 340 : state.Bottom;
        state.Legend.StretchMode = bilingual ? TextureRect.StretchModeEnum.Scale : state.StretchMode;
        state.Items.OffsetTop = bilingual ? 116 : state.ItemsTop;
        state.Items.OffsetBottom = bilingual ? 596 : state.ItemsBottom;
        Fit(state.Header, new Rect2(47, 20, 246, 88));
        foreach (var row in state.Rows)
        {
            row.Item.CustomMinimumSize = bilingual ? new Vector2(row.Minimum.X, 80) : row.Minimum;
            Fit(row.Text, new Rect2(84, 3, 185, 74));
        }
    }

    private static void Fit(TextSlot slot, Rect2 area)
    {
        slot.Original.Visible = !LanguageSwitcher.IsBilingual && slot.Visible;
        var rich = slot.Bilingual;
        rich.Visible = LanguageSwitcher.IsBilingual && slot.Visible;
        if (!LanguageSwitcher.IsBilingual) return;
        BilingualFonts.Restore(rich);
        foreach (var name in new[] { "normal_font", "bold_font", "italics_font", "bold_italics_font" })
            rich.AddThemeFontOverride(name, slot.Original.GetThemeFont("font"));
        BilingualFonts.Apply(rich, rich.Text);
        rich.Size = area.Size;
        for (var size = slot.MaxFontSize; size >= 8; size--)
        {
            SetSize(rich, size);
            LiveText.RefreshControl(rich);
            if (rich.GetContentWidth() <= area.Size.X && rich.GetContentHeight() <= area.Size.Y) break;
        }
        rich.Position = area.Position + new Vector2(0, (area.Size.Y - rich.GetContentHeight()) / 2);
    }

    private static void SetSize(RichTextLabel label, int size)
    {
        foreach (var name in new[] { "normal_font_size", "bold_font_size", "italics_font_size", "bold_italics_font_size" })
            label.AddThemeFontSizeOverride(name, size);
    }
}
