using BilingualSpire.Localization;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;

namespace BilingualSpire.Patches;

[HarmonyPatch(typeof(MegaRichTextLabel), "SetFontSize")]
internal static class SecondaryTextSizePatch
{
    private static void Postfix(MegaRichTextLabel __instance)
    {
        // The game estimates layout at the primary size. Keep the translation proportional
        // after its deferred auto-sizing, including card updates and viewport changes.
        if (!LanguageSwitcher.IsBilingual || !SecondaryTextStyle.AppliesTo(__instance)) return;
        try { LiveText.RefreshControl(__instance); }
        catch (Exception error) { LiveText.Report(error); }
    }
}
