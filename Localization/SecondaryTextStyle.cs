using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Events;

namespace BilingualSpire.Localization;

internal static class SecondaryTextStyle
{
    internal static bool AppliesTo(Control control)
    {
        if (control is not RichTextLabel) return false;
        if (control.Name == "BilingualLegendText") return true;
        for (var parent = control.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is NCard or NEventLayout) return true;
        return false;
    }

    internal static int FontSize(Control control) => Math.Max(1,
        (int)Math.Round(control.GetThemeFontSize("normal_font_size") * 0.85, MidpointRounding.AwayFromZero));
}
