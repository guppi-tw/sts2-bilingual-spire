using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace BilingualSpire.Localization;

internal static class BilingualFonts
{
    private sealed record FontOverride(Font Original, bool HadOverride, Font Applied);
    private sealed class FontState
    {
        internal readonly Dictionary<string, FontOverride> Overrides = new();
        internal string Language = "";
        internal int? MaxLines;
        internal int FontSize;
        internal bool HadFontSizeOverride;
        internal float Height;
        internal float Width;
        internal (float Left, float Right, float OffsetLeft, float OffsetRight) Horizontal;
        internal (bool AutoSize, int Min, int Max)? AutoSize;
    }
    private static readonly ConditionalWeakTable<Control, FontState> States = new();

    internal static void Apply(Control control, string text)
    {
        if (!LanguageSwitcher.IsBilingual) { Restore(control); return; }
        var language = LanguageSwitcher.Preferences.Second;
        var state = States.GetOrCreateValue(control);
        if (state.Language != language) { RestoreFonts(control, state); state.Language = language; }
        if (control is Label label && state.MaxLines is null)
        {
            state.MaxLines = label.MaxLinesVisible;
            state.FontSize = label.GetThemeFontSize("font_size");
            state.HadFontSizeOverride = label.HasThemeFontSizeOverride("font_size");
            state.Height = label.Size.Y;
            state.Width = label.Size.X;
            state.Horizontal = (label.AnchorLeft, label.AnchorRight, label.OffsetLeft, label.OffsetRight);
            if (label is MegaLabel mega) state.AutoSize = (mega.AutoSizeEnabled, mega.MinFontSize, mega.MaxFontSize);
            label.MaxLinesVisible = -1;
        }
        var fonts = control is RichTextLabel
            ? new[] { ("normal_font", FontType.Regular), ("bold_font", FontType.Bold), ("italics_font", FontType.Italic) }
            : new[] { ("font", FontType.Regular) };
        foreach (var (name, type) in fonts)
        {
            var current = control.GetThemeFont(name);
            if (state.Overrides.TryGetValue(name, out var existing) && current == existing.Applied) continue;
            var secondary = FontManager.GetSubstituteFont(language, type);
            if (secondary is null || secondary == current) continue;
            var font = new FontVariation { BaseFont = current, Fallbacks = [secondary] };
            state.Overrides[name] = new(current, control.HasThemeFontOverride(name), font);
            control.AddThemeFontOverride(name, font);
        }
        if (control is Label sized && state.MaxLines is not null)
        {
            if (control.Name == "TypeLabel")
            {
                sized.AddThemeFontSizeOverride("font_size", state.FontSize);
                if (sized is MegaLabel typeLabel) typeLabel.AutoSizeEnabled = false;
                var width = sized.GetThemeFont("font").GetStringSize(text, fontSize: state.FontSize).X + 4;
                sized.AnchorLeft = sized.AnchorRight = 0.5f;
                sized.OffsetLeft = -width / 2;
                sized.OffsetRight = width / 2;
                ResizeTypePlaque(control);
                return;
            }
            var lines = text.Count(c => c == '\n') + 1;
            var lineHeight = sized.GetThemeFont("font").GetHeight(state.FontSize);
            var available = state.Height > 0 ? state.Height : lineHeight;
            var size = Math.Clamp((int)Math.Floor(state.FontSize * available / (lineHeight * lines + Math.Max(0, lines - 1) * sized.GetThemeConstant("line_spacing"))), 8, Math.Max(8, state.FontSize));
            if (sized is MegaLabel mega)
            {
                mega.AutoSizeEnabled = true;
                mega.MinFontSize = Math.Min(state.AutoSize!.Value.Min, size);
                mega.MaxFontSize = size;
            }
            sized.AddThemeFontSizeOverride("font_size", size);
        }
    }

    internal static void Restore(Control control)
    {
        if (!States.TryGetValue(control, out var state)) return;
        RestoreFonts(control, state);
        if (control is Label label && state.MaxLines is { } lines)
        {
            label.MaxLinesVisible = lines;
            if (state.HadFontSizeOverride) label.AddThemeFontSizeOverride("font_size", state.FontSize);
            else label.RemoveThemeFontSizeOverride("font_size");
            if (label is MegaLabel mega && state.AutoSize is { } sizing)
            {
                mega.AutoSizeEnabled = sizing.AutoSize;
                mega.MinFontSize = sizing.Min;
                mega.MaxFontSize = sizing.Max;
            }
            if (control.Name == "TypeLabel")
            {
                label.AnchorLeft = state.Horizontal.Left;
                label.AnchorRight = state.Horizontal.Right;
                label.OffsetLeft = state.Horizontal.OffsetLeft;
                label.OffsetRight = state.Horizontal.OffsetRight;
                ResizeTypePlaque(control);
            }
        }
        States.Remove(control);
    }

    private static void RestoreFonts(Control control, FontState state)
    {
        foreach (var (name, font) in state.Overrides)
        {
            if (control.GetThemeFont(name) != font.Applied) continue;
            if (font.HadOverride) control.AddThemeFontOverride(name, font.Original);
            else control.RemoveThemeFontOverride(name);
        }
        state.Overrides.Clear();
    }

    private static void ResizeTypePlaque(Control control)
    {
        for (var parent = control.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is NCard card) { card.CallDeferred("UpdateTypePlaqueSizeAndPosition"); break; }
    }
}
