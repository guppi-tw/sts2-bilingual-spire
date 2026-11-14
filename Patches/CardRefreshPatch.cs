using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.addons.mega_text;
using BilingualSpire.Localization;

namespace BilingualSpire.Patches;

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class CardRefreshPatch
{
    private sealed record DisplayState(PileType Pile, CardPreviewMode Preview, Creature? Target);
    private static readonly ConditionalWeakTable<NCard, DisplayState> Cards = new();

    private static void Postfix(NCard __instance, PileType pileType, CardPreviewMode previewMode,
        MegaLabel ____titleLabel, MegaRichTextLabel ____descriptionLabel, Creature? ____previewTarget)
    {
        if (!__instance.IsNodeReady()) return;
        Cards.Remove(__instance);
        Cards.Add(__instance, new(pileType, previewMode, ____previewTarget));
        CardHoverPatch.Refresh(__instance);
        if (__instance.Visibility != ModelVisibility.Visible || __instance.Model is not { } model) return;
        try
        {
            var target = ____previewTarget ?? model.CurrentTarget;
            LiveText.BindRenderer(____titleLabel, () => model.Title);
            LiveText.BindRenderer(____descriptionLabel, () => $"[center]{(previewMode == CardPreviewMode.Upgrade
                ? model.GetDescriptionForUpgradePreview() : model.GetDescriptionForPile(pileType, target))}[/center]");
        }
        catch (Exception error) { LiveText.Report(error); }
    }

    internal static string Description(NCard card)
    {
        if (!Cards.TryGetValue(card, out var state) || card.Model is not { } model) return "";
        return state.Preview == CardPreviewMode.Upgrade ? model.GetDescriptionForUpgradePreview()
            : model.GetDescriptionForPile(state.Pile, state.Target ?? model.CurrentTarget);
    }

    internal static void Refresh()
    {
        // Card keywords add language-dependent punctuation outside LocString formatting.
        foreach (var (card, state) in Cards.ToArray())
        {
            if (!GodotObject.IsInstanceValid(card) || card.IsQueuedForDeletion()) continue;
            try { card.UpdateVisuals(state.Pile, state.Preview); }
            catch (Exception error) { Localization.LiveText.Report(error); }
        }
    }
}
