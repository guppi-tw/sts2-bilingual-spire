using BilingualSpire.Localization;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace BilingualSpire.Patches;

[HarmonyPatch(typeof(LocString), nameof(LocString.GetFormattedText))]
internal static class FormattedTextPatch
{
    private static void Postfix(LocString __instance, string __result)
    {
        try { LiveText.Capture(__instance, __result, raw: false); }
        catch (Exception error) { LiveText.Report(error); }
    }
}

[HarmonyPatch(typeof(LocString), nameof(LocString.GetRawText))]
internal static class RawTextPatch
{
    private static void Postfix(LocString __instance, string __result)
    {
        try { LiveText.Capture(__instance, __result, raw: true); }
        catch (Exception error) { LiveText.Report(error); }
    }
}

[HarmonyPatch]
internal static class LabelTextPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertySetter(typeof(Label), nameof(Label.Text));
        yield return AccessTools.PropertySetter(typeof(RichTextLabel), nameof(RichTextLabel.Text));
        yield return AccessTools.PropertySetter(typeof(Button), nameof(Button.Text));
    }

    private static void Prefix(Control __instance, ref string __0)
    {
        try { __0 = LiveText.Prepare(__instance, __0); }
        catch (Exception error) { LiveText.Report(error); }
    }
}
