using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.addons.mega_text;

namespace BilingualSpire.Patches;

[HarmonyPatch(typeof(NAncientNameBanner), "AnimateVfx")]
internal static class AncientBannerPatch
{
    private static readonly ConditionalWeakTable<NAncientNameBanner, object> Banners = new();

    private static void Postfix(NAncientNameBanner __instance, ref Task __result)
    {
        Banners.GetOrCreateValue(__instance);
        __result = AfterAnimation(__instance, __result);
        Apply(__instance);
    }

    private static async Task AfterAnimation(NAncientNameBanner banner, Task animation)
    {
        await animation;
        Apply(banner);
    }

    internal static void Refresh()
    {
        foreach (var (banner, _) in Banners.ToArray()) Apply(banner);
    }

    private static void Apply(NAncientNameBanner banner)
    {
        if (!GodotObject.IsInstanceValid(banner) || banner.IsQueuedForDeletion() || !banner.IsInsideTree()) return;
        var title = banner.GetNode<MegaRichTextLabel>("%Title");
        var epithet = banner.GetNode<MegaLabel>("%Epithet");
        // The native banner places the epithet below a single title; extra lines grow upward.
        var extraLines = LanguageSwitcher.IsBilingual ? epithet.Text.Count(c => c == '\n') : 0;
        var spacing = epithet.GetThemeFont("font").GetHeight(epithet.GetThemeFontSize("font_size"));
        title.Position = new Vector2(title.Position.X, -extraLines * spacing);
    }
}
