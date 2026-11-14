using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace BilingualSpire.Patches;

// Observe the user's settings action, not temporary locale changes used by game metrics or the mod.
[HarmonyPatch(typeof(NLanguageDropdown), "OnDropdownItemSelected")]
internal static class GameLanguagePatch
{
    private static void Prefix(NDropdownItem __0, out string? __state)
    {
        if (__0 is NLanguageDropdownItem) LanguageSwitcher.EndPeek();
        __state = __0 is NLanguageDropdownItem item && item.LanguageCode != LocManager.Instance.Language
            ? item.LanguageCode : null;
    }

    private static void Postfix(string? __state)
    {
        if (__state is null) return;
        try { LanguageSwitcher.UseGameLanguage(__state); }
        catch (Exception error)
        {
            ModEntry.Logger.Warn($"Could not synchronize game language: {error}");
            LanguageSwitcher.Overlay.ShowStatus("本体の言語設定をMODに反映できませんでした", 5);
        }
    }
}
