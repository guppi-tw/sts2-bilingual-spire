#if DEBUG
using BilingualSpire.Localization;
using BilingualSpire.Settings;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.addons.mega_text;

namespace BilingualSpire.Development;

internal static class GameLanguageSmokeTest
{
    internal static async Task Run()
    {
        var game = NGame.Instance!;
        LanguageSwitcher.SetPair("eng", "jpn");
        LanguageSwitcher.ShowBilingual();
        var card = ModelDb.Card<StrikeIronclad>().ToMutable();
        var view = NCard.Create(card)!;
        game.AddChild(view);
        view.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        try
        {
            await Select("jpn");
            Require(LanguageSwitcher.Preferences == new LanguagePreferences("jpn", "eng", DisplayMode.Bilingual), "Selecting the second language swaps the pair without losing bilingual mode");
            Require(view.GetNode<MegaLabel>("%TitleLabel").Text == "ストライク\nStrike", "Existing card follows the new language order");
            await Select("fra");
            Require(LanguageSwitcher.Preferences == new LanguagePreferences("fra", "eng", DisplayMode.Bilingual), "A new game language replaces only the first language");
            var french = card.Title;
            var english = ScopedLocale.Render("eng", () => card.Title);
            Require(view.GetNode<MegaLabel>("%TitleLabel").Text == french + "\n" + english, "Existing card still has two distinct languages");
            var fresh = NCard.Create(card)!;
            game.AddChild(fresh);
            fresh.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
            Require(fresh.GetNode<MegaLabel>("%TitleLabel").Text == french + "\n" + english, "New card follows the synchronized pair");
            fresh.QueueFree();

            LanguageSwitcher.SwitchTo("eng");
            await Select("kor");
            Require(LanguageSwitcher.Preferences == new LanguagePreferences("kor", "eng", DisplayMode.First) && !LanguageSwitcher.IsBilingual,
                "Single-language mode follows the user's game language choice");
            Require(view.GetNode<MegaLabel>("%TitleLabel").Text == card.Title && !card.Title.Contains('\n'), "Single-language card uses the chosen language");
            LanguageSwitcher.Toggle();
            Require(LocManager.Instance.Language == "eng" && LanguagePreferences.Load().Mode == DisplayMode.Second, "C cycle continues with the synchronized second language");
            LanguageSwitcher.Toggle();
            Require(LanguageSwitcher.IsBilingual && LanguagePreferences.Load().Mode == DisplayMode.Bilingual, "C returns to bilingual mode");
            Require(SaveManager.Instance.SettingsSave.Language == "kor", "C does not overwrite the game's saved language");
            ModEntry.Logger.Info("GAME LANGUAGE PASS: native settings action, pair swapping, new language, existing/new cards, persistence and C cycle");
        }
        finally
        {
            view.QueueFree();
            LanguageSwitcher.SetPair("eng", "jpn");
            LanguageSwitcher.SwitchTo("eng");
        }
    }

    private static async Task Select(string language)
    {
        var game = NGame.Instance!;
        game.MainMenu!.OpenSettingsMenu();
        await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
        var dropdown = Descendants(game).OfType<NLanguageDropdown>().First(d => d.IsVisibleInTree());
        // Use the actual settings callback so persistence, menu reconstruction and the Harmony hook all run.
        var item = Descendants(dropdown).OfType<NLanguageDropdownItem>().First(i => i.LanguageCode == language);
        dropdown.Call("OnDropdownItemSelected", item);
        await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
        await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(LocManager.Instance.Language == language && SaveManager.Instance.SettingsSave.Language == language, "Native settings selected the requested language");
        Require(LanguagePreferences.Load() == LanguageSwitcher.Preferences, "Synchronized pair and mode were saved");
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        yield return node;
        foreach (var child in node.GetChildren()) foreach (var item in Descendants(child)) yield return item;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
