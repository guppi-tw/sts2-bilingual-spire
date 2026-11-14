#if DEBUG
using BilingualSpire.Settings;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.addons.mega_text;

namespace BilingualSpire.Development;

internal static class SmokeTest
{
    internal static async Task Run()
    {
        var game = NGame.Instance!;
        try
        {
            await LanguageSwitcher.Ready;
            // Deliver synthetic press/release events separately even when a frame stalls.
            Input.UseAccumulatedInput = false;
            await Frame();
            ParagraphSmokeTest.Run();
            if (System.Environment.GetEnvironmentVariable("BILINGUAL_SPIRE_TEST_PHASE") is { } phase && phase != "full")
            {
                if (phase == "hold")
                {
                    await HoldTranslationSmokeTest.Run();
                    ModEntry.Logger.Info("SMOKE TEST PASS: hold-to-translate, release, focus loss, persistence and manual changes");
                    game.GetTree().Quit(0);
                    return;
                }
                if (phase == "readability")
                {
                    await ReadabilitySmokeTest.Run();
                    ModEntry.Logger.Info("SMOKE TEST PASS: card hover and menu readability");
                    game.GetTree().Quit(0);
                    return;
                }
                if (phase == "neow")
                {
                    await NeowProgressionSmokeTest.Run();
                    ModEntry.Logger.Info("SMOKE TEST PASS: Neow reward and map progression");
                    game.GetTree().Quit(0);
                    return;
                }
                if (phase == "story")
                {
                    await StorySmokeTest.Run();
                    ModEntry.Logger.Info("SMOKE TEST PASS: isolated event layouts");
                    game.GetTree().Quit(0);
                    return;
                }
                if (phase == "native")
                {
                    await GameLanguageSmokeTest.Run();
                    ModEntry.Logger.Info("SMOKE TEST PASS: native game language synchronization");
                    game.GetTree().Quit(0);
                    return;
                }
                await PersistenceSmokeTest.Run(phase);
                game.GetTree().Quit(0);
                return;
            }
            await CheckFirstLaunch();
            LanguageSwitcher.SwitchTo("jpn");
            var savedLanguage = SaveManager.Instance.SettingsSave.Language;
            var menu = game.MainMenu;
            Require(menu is not null, "Main menu is ready");
            var menuText = Descendants(menu!).OfType<Control>()
                .Where(c => c is Label or RichTextLabel)
                .ToDictionary(c => c, c => Localization.LiveText.GetText(c));
            var cards = new List<(CardModel Model, NCard View)>();
            foreach (var id in new[] { "STRIKE_IRONCLAD", "DEFEND_IRONCLAD", "BASH", "ASCENDERS_BANE", "CORRUPTION", "WHIRLWIND" })
            {
                var card = ModelDb.AllCards.First(c => c.Id.Entry == id).ToMutable();
                var view = NCard.Create(card)!;
                game.AddChild(view);
                view.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
                cards.Add((card, view));
            }
            CheckCards(cards);
            var originalJapanese = cards[0].View.GetNode<MegaLabel>("%TitleLabel").Text;
            Require(originalJapanese == "ストライク", "Japanese fixture text");

            var examples = new List<(LocString Source, Label Label)>();
            foreach (var tableName in new[] { "events", "relics", "static_hover_tips", "main_menu_ui" })
            {
                var table = LocManager.Instance.GetTable(tableName);
                var key = table.Keys.First(k => !table.GetRawText(k).Contains('{') && table.GetRawText(k).Length > 8);
                var source = new LocString(tableName, key);
                var label = new Label { Text = source.GetFormattedText() };
                game.AddChild(label);
                examples.Add((source, label));
            }
            var locked = NCard.Create(cards[0].Model, ModelVisibility.Locked)!;
            game.AddChild(locked);
            locked.UpdateVisuals(PileType.None, CardPreviewMode.Normal);

            LanguageSwitcher.SwitchTo("kor");
            await KeyDown(Key.F8);
            Require(LocManager.Instance.Language == "eng", "F8 switches to English");
            await Frame();
            await Frame();
            Require(LocManager.Instance.Language == "eng", "Holding F8 does not repeat");
            await KeyUp(Key.F8);
            CheckCards(cards);
            Require(cards[0].View.GetNode<MegaLabel>("%TitleLabel").Text == "Strike", "English replaces Japanese in the existing label");
            Require(menu == game.MainMenu, "Language switch preserves the current menu instance");
            var changed = menuText.Count(p => p.Value != Localization.LiveText.GetText(p.Key));
            Require(changed >= 4, $"Actual main menu labels updated (count={changed})");
            foreach (var (source, label) in examples)
                Require(label.Text == source.GetFormattedText(), $"{source.LocTable}: existing label relocalized");
            Require(!locked.GetNode<MegaLabel>("%TitleLabel").Text.Contains("Strike"), "Locked card stays hidden");
            Require(!locked.GetNode<MegaRichTextLabel>("%DescriptionLabel").Text.Contains('6'), "Locked description stays hidden");

            await Press(Key.F8);
            Require(LocManager.Instance.Language == "jpn", "F8 switches back to Japanese");
            CheckCards(cards);
            Require(cards[0].View.GetNode<MegaLabel>("%TitleLabel").Text == originalJapanese, "Round trip restores original title");

            cards[0].Model.UpgradeInternal();
            cards[0].Model.FinalizeUpgradeInternal();
            cards[0].View.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
            LanguageSwitcher.SwitchTo("eng");
            CheckCards(cards);
            Require(cards[0].View.GetNode<MegaLabel>("%TitleLabel").Text == "Strike+", "Upgrade suffix preserved");
            Require(cards[0].View.GetNode<MegaRichTextLabel>("%DescriptionLabel").Text.Contains('9'), "Upgraded damage preserved");

            // Two displays of the same model can intentionally show different preview values.
            var damage = cards[0].Model.DynamicVars["Damage"];
            var snapshotSource = cards[0].Model.Description;
            cards[0].Model.DynamicVars.AddTo(snapshotSource);
            var snapshot = new Label { Text = snapshotSource.GetFormattedText() };
            game.AddChild(snapshot);
            var preview = damage.PreviewValue;
            damage.PreviewValue = 99;
            await Press(Key.F8);
            Require(snapshot.Text.Contains('9') && !snapshot.Text.Contains("99"), "Recorded damage is independent of later preview mutations");
            damage.PreviewValue = preview;

            await Press(Key.F9);
            Require(LanguageSwitcher.Overlay.SettingsVisible, "F9 opens language settings");
            Require(LanguageSwitcher.Overlay.LanguageCount == LanguagePreferences.Languages.Length && LanguageSwitcher.Overlay.LanguageCount >= 16, "Game languages available in dropdowns");
            if (System.Environment.GetEnvironmentVariable("BILINGUAL_SPIRE_SCREENSHOT") is { } screenshot)
            {
                // Hide test fixtures so this captures the real menu and the mod's panel.
                foreach (var (_, view) in cards) view.Hide();
                foreach (var (_, label) in examples) label.Hide();
                locked.Hide();
                snapshot.Hide();
                await game.ToSignal(game.GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
                await Frame();
                RenderingServer.ForceDraw();
                await Frame();
                RenderingServer.ForceDraw();
                Require(game.GetViewport().GetTexture().GetImage().SavePng(screenshot) == Error.Ok, "Settings screenshot saved");
            }
            await Press(Key.F9);
            Require(!LanguageSwitcher.Overlay.SettingsVisible, "F9 closes language settings");
            LanguageSwitcher.SetPair("fra", "kor");
            Require(LanguagePreferences.Load().First == "fra" && LanguagePreferences.Load().Second == "kor", "Language pair saved and reloaded");
            LanguageSwitcher.SwitchTo("jpn");
            await Press(Key.F8);
            Require(LocManager.Instance.Language == "fra", "Language outside pair switches to the first selection");
            CheckCards(cards);

            await Press(Key.F8);
            Require(LocManager.Instance.Language == "kor", "Second chosen language can be Korean");
            CheckCards(cards);

            foreach (var language in LanguagePreferences.Languages)
            {
                LanguageSwitcher.SwitchTo(language);
                CheckCards(cards);
            }

            var overwritten = examples[0].Label;
            overwritten.Text = "Player supplied text";
            LanguageSwitcher.SetPair("eng", "jpn");
            await Press(Key.F8);
            Require(overwritten.Text == "Player supplied text", "Nonlocalized replacement is not translated");
            var editor = new LineEdit();
            game.AddChild(editor);
            editor.GrabFocus();
            await Press(Key.F8);
            Require(LocManager.Instance.Language == "eng", "Shortcut ignored while entering text");
            editor.ReleaseFocus();

            await CheckBilingualMode(cards, examples, locked);

            Require(SaveManager.Instance.SettingsSave.Language == savedLanguage, "Game's persisted language preference is unchanged");
            Require(new LanguagePreferences("unknown", "unknown").Validated() == new LanguagePreferences(), "Invalid language settings fall back safely");
            Require(new LanguagePreferences("jpn", "jpn").Validated() == new LanguagePreferences("jpn", "eng"), "Duplicate language settings repaired");
            foreach (var (_, view) in cards) view.Hide();
            foreach (var (_, label) in examples) label.Hide();
            locked.Hide();
            snapshot.Hide();
            editor.Hide();
            await GameLanguageSmokeTest.Run();
            await StorySmokeTest.Run();
            LanguageSwitcher.SetPair("eng", "jpn");
            LanguageSwitcher.ShowBilingual();
            Require(LanguagePreferences.Load().Mode == DisplayMode.Bilingual, "Bilingual display mode persisted for restart test");
            ModEntry.Logger.Info($"SMOKE TEST PASS: {cards.Count} cards, upgrades, hidden cards, {changed} menu labels, event/relic/tooltips, real event layouts and story progression, F8/F9 three-mode cycle, bilingual updates, {LanguagePreferences.Languages.Length} languages, preferences and save isolation");
            game.GetTree().Quit(0);
        }
        catch (Exception error)
        {
            ModEntry.Logger.Error($"SMOKE TEST FAIL: {error}");
            game.GetTree().Quit(1);
        }
    }

    private static async Task CheckBilingualMode(List<(CardModel Model, NCard View)> cards,
        List<(LocString Source, Label Label)> examples, NCard locked)
    {
        var game = NGame.Instance!;
        var scene = game.MainMenu!;
        LanguageSwitcher.SwitchTo("eng");
        await Frame();
        var readability = ReadabilityAudit.Capture(game);
        var english = cards.Select(c => (c.Model.Title, Description(c.Model))).ToArray();
        var englishEvent = examples[1].Source.GetFormattedText();
        await Press(Key.F8);
        Require(LocManager.Instance.Language == "jpn" && !LanguageSwitcher.IsBilingual, "Cycle: first -> second");
        var japanese = cards.Select(c => (c.Model.Title, Description(c.Model))).ToArray();
        var japaneseEvent = examples[1].Source.GetFormattedText();
        await KeyDown(Key.F8);
        Require(LanguageSwitcher.IsBilingual && LocManager.Instance.Language == "eng", "Cycle: second -> bilingual with first as base");
        await Frame();
        Require(LanguageSwitcher.IsBilingual, "Held key keeps bilingual mode");
        await KeyUp(Key.F8);
        for (var i = 0; i < cards.Count; i++)
        {
            Require(cards[i].View.GetNode<MegaLabel>("%TitleLabel").Text == Both(english[i].Title, japanese[i].Title), $"{cards[i].Model.Id}: bilingual title in selected order");
            Require(SecondaryTextSmokeTest.WithoutSize(cards[i].View.GetNode<MegaRichTextLabel>("%DescriptionLabel").Text) == Both(english[i].Item2, japanese[i].Item2), $"{cards[i].Model.Id}: full bilingual description and punctuation");
        }
        Require(examples[1].Label.Text == Both(englishEvent, japaneseEvent), "Existing non-card text becomes bilingual");
        var newLabel = new Label { Text = examples[1].Source.GetFormattedText() };
        game.AddChild(newLabel);
        Require(newLabel.Text == Both(englishEvent, japaneseEvent), "New text is bilingual while the mode stays enabled");
        for (var i = 0; i < 3; i++) newLabel.Text = examples[1].Source.GetFormattedText();
        Require(newLabel.Text == Both(englishEvent, japaneseEvent), "Repeated updates do not duplicate translations");
        var newView = NCard.Create(cards[2].Model)!;
        game.AddChild(newView);
        newView.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        Require(SecondaryTextSmokeTest.WithoutSize(newView.GetNode<MegaRichTextLabel>("%DescriptionLabel").Text) == Both(english[2].Item2, japanese[2].Item2), "New card is bilingual");
        Require(newView.GetNode<MegaLabel>("%TitleLabel").GetThemeFont("font").HasChar('ス'), "Combined font includes Japanese glyphs");
        await Frame();
        await Frame();
        foreach (var (_, view) in cards) SecondaryTextSmokeTest.Check(view.GetNode<MegaRichTextLabel>("%DescriptionLabel"));
        SecondaryTextSmokeTest.Check(newView.GetNode<MegaRichTextLabel>("%DescriptionLabel"));
        ReadabilityAudit.Report("menus/cards", readability);
        var typeLabel = newView.GetNode<MegaLabel>("%TypeLabel");
        var plaque = newView.GetNode<Control>("%TypePlaque");
        Require(Math.Abs(typeLabel.GetGlobalRect().GetCenter().X - plaque.GetGlobalRect().GetCenter().X) < 2, "Bilingual card type stays centered on its plaque");
        var firstFontSize = cards[0].View.GetNode<MegaLabel>("%TitleLabel").GetThemeFontSize("font_size");
        for (var i = 0; i < 3; i++) cards[0].View.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        Require(cards[0].View.GetNode<MegaLabel>("%TitleLabel").GetThemeFontSize("font_size") == firstFontSize, "Repeated bilingual refreshes do not shrink text further");
        Require(!locked.GetNode<MegaLabel>("%TitleLabel").Text.Contains("Strike"), "Bilingual mode does not reveal locked cards");
        Require(game.MainMenu == scene, "Bilingual mode preserves the current scene");
        Require(!Localization.ScopedLocale.IsActive, "Secondary locale does not leak");
        try { Localization.ScopedLocale.Render("jpn", () => throw new InvalidOperationException("scope test")); }
        catch (InvalidOperationException) { }
        Require(!Localization.ScopedLocale.IsActive && LocManager.Instance.Language == "eng", "Secondary locale restores after an exception");

        if (System.Environment.GetEnvironmentVariable("BILINGUAL_SPIRE_SCREENSHOT") is { } screenshot)
        {
            newLabel.Hide();
            newView.Hide();
            foreach (var (_, view) in cards) view.Hide();
            LanguageSwitcher.Overlay.ToggleSettings();
            await game.ToSignal(game.GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
            await Frame();
            RenderingServer.ForceDraw();
            Require(game.GetViewport().GetTexture().GetImage().SavePng(Path.ChangeExtension(screenshot, "bilingual.png")) == Error.Ok, "Bilingual menu screenshot saved");
            LanguageSwitcher.Overlay.ToggleSettings();
            scene.Hide();
            for (var i = 0; i < cards.Count; i++)
            {
                var view = cards[i].View;
                view.Position = new Vector2(330 + i % 3 * 620, 350 + i / 3 * 470);
                view.Show();
            }
            await Frame();
            RenderingServer.ForceDraw();
            await Frame();
            RenderingServer.ForceDraw();
            Require(game.GetViewport().GetTexture().GetImage().SavePng(Path.ChangeExtension(screenshot, "cards.png")) == Error.Ok, "Bilingual card screenshot saved");
            foreach (var (_, view) in cards) view.Hide();
            scene.Show();
        }
        await Press(Key.F8);
        Require(!LanguageSwitcher.IsBilingual && LocManager.Instance.Language == "eng", "Cycle: bilingual -> first");
        CheckCards(cards);
        Require(newLabel.Text == englishEvent, "Leaving bilingual mode removes second-language text");

        LanguageSwitcher.SwitchTo("fra");
        var french = cards[0].Model.Title;
        LanguageSwitcher.SwitchTo("kor");
        var korean = cards[0].Model.Title;
        LanguageSwitcher.ShowBilingual();
        LanguageSwitcher.SetPair("fra", "kor");
        Require(LanguageSwitcher.IsBilingual && cards[0].View.GetNode<MegaLabel>("%TitleLabel").Text == Both(french, korean), "Changing the pair refreshes bilingual text immediately");
        LanguageSwitcher.SwitchTo("fra");
        CheckCards(cards);
        LanguageSwitcher.SetPair("eng", "jpn");
        LanguageSwitcher.SwitchTo("eng");
        newLabel.QueueFree();
        newView.QueueFree();
    }

    private static string Description(CardModel card) => $"[center]{card.GetDescriptionForPile(PileType.None)}[/center]";
    private static string Both(string first, string second) => Localization.LiveText.Combine(first, second);

    private static void CheckCards(List<(CardModel Model, NCard View)> cards)
    {
        foreach (var (model, view) in cards)
        {
            var title = view.GetNode<MegaLabel>("%TitleLabel");
            var description = view.GetNode<MegaRichTextLabel>("%DescriptionLabel");
            Require(title.Text == model.Title, $"{model.Id}: title in {LocManager.Instance.Language}: {title.Text} != {model.Title}");
            var expected = $"[center]{model.GetDescriptionForPile(PileType.None)}[/center]";
            Require(description.Text == expected, $"{model.Id}: description in {LocManager.Instance.Language}: {description.Text} != {expected}");
            Require(!description.Text.Contains('{'), $"{model.Id}: no unresolved format variables");
        }
    }

    private static async Task CheckFirstLaunch()
    {
        Require(!File.Exists(LanguagePreferences.ConfigPath), "First-launch fixture has no saved mod preferences");
        Require(LocManager.Instance.Language == "eng" && !LanguageSwitcher.IsBilingual,
            "First launch starts in the learning language even when the game language is Japanese");
        var game = NGame.Instance!;
        var view = NCard.Create(ModelDb.AllCards.First(c => c.Id.Entry == "STRIKE_IRONCLAD").ToMutable())!;
        game.AddChild(view);
        view.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        var title = view.GetNode<MegaLabel>("%TitleLabel");
        Require(title.Text == "Strike", "First-launch card is in English");
        await KeyDown(Key.F7);
        Require(title.Text == "ストライク" && LanguageSwitcher.IsPeeking, "Default controls show Japanese while F7 is held");
        await KeyUp(Key.F7);
        await Frame();
        Require(title.Text == "Strike" && !LanguageSwitcher.IsPeeking, "Default controls return to English on release");
        Require(!File.Exists(LanguagePreferences.ConfigPath) && SaveManager.Instance.SettingsSave.Language == "jpn",
            "The initial default and temporary translation do not overwrite native or mod settings");
        LanguageSwitcher.SetPair("fra", "kor");
        Require(LocManager.Instance.Language == "fra" && LanguagePreferences.Load().Mode == DisplayMode.First,
            "Choosing a language pair on first launch immediately displays its learning language");
        LanguageSwitcher.SetPair("eng", "jpn");
        view.QueueFree();
        await Frame();
        ModEntry.Logger.Info("DEFAULT MODE PASS: first launch in English, hold F7 for Japanese, release to English; native settings unchanged");
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        yield return node;
        foreach (var child in node.GetChildren())
            foreach (var descendant in Descendants(child)) yield return descendant;
    }

    private static async Task Frame() => await NGame.Instance!.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
    private static async Task KeyDown(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        await Frame();
        await Frame();
    }
    private static async Task KeyUp(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        await Frame();
    }
    private static async Task Press(Key key) { await KeyDown(key); await KeyUp(key); }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
