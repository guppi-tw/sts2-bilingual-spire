#if DEBUG
using BilingualSpire.Settings;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.addons.mega_text;

namespace BilingualSpire.Development;

internal static class HoldTranslationSmokeTest
{
    internal static async Task Run()
    {
        var game = NGame.Instance!;
        foreach (var action in InputMap.GetActions())
            foreach (var key in InputMap.ActionGetEvents(action).OfType<InputEventKey>())
                Require((key.Keycode != Key.F7 && key.PhysicalKeycode != Key.F7) || key.AltPressed || key.CtrlPressed || key.MetaPressed || key.ShiftPressed,
                    "F7 does not conflict with a plain game binding: " + action);
        LanguageSwitcher.SetPair("eng", "jpn");
        var card = NCard.Create(ModelDb.Card<StrikeIronclad>().ToMutable())!;
        game.AddChild(card);
        card.Position = new Vector2(1400, 500);
        card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        var title = card.GetNode<MegaLabel>("%TitleLabel");
        var nativeLanguage = SaveManager.Instance.SettingsSave.Language;

        var iconSizes = new Dictionary<DisplayMode, Vector2>();
        foreach (var mode in new[] { DisplayMode.First, DisplayMode.Second, DisplayMode.Bilingual })
        {
            if (mode == DisplayMode.Bilingual) LanguageSwitcher.ShowBilingual();
            else LanguageSwitcher.SwitchTo(mode == DisplayMode.First ? "eng" : "jpn");
            await Frames();
            var original = title.Text;
            iconSizes[mode] = ProfileIcon().Size;
            var saved = System.IO.File.ReadAllText(LanguagePreferences.ConfigPath);
            var modified = System.IO.File.GetLastWriteTimeUtc(LanguagePreferences.ConfigPath);
            await SendKey(Key.F7, true);
            Require(LanguageSwitcher.IsPeeking && !LanguageSwitcher.IsBilingual && title.Text == "ストライク", $"F7 shows only the mother tongue from {mode}: peek={LanguageSwitcher.IsPeeking}, title={title.Text}, focused={game.GetWindow().HasFocus()}");
            await Frames();
            Require(title.Text == "ストライク", "Holding does not repeat or cycle");
            if (mode == DisplayMode.First) await Screenshot("held");
            await SendKey(Key.F7, false);
            Require(!LanguageSwitcher.IsPeeking && title.Text == original && LanguageSwitcher.IsBilingual == (mode == DisplayMode.Bilingual), "Release restores the exact previous mode: " + mode);
            Require(System.IO.File.ReadAllText(LanguagePreferences.ConfigPath) == saved
                && System.IO.File.GetLastWriteTimeUtc(LanguagePreferences.ConfigPath) == modified, "A temporary peek never writes saved preferences");
            if (mode != DisplayMode.Bilingual) CheckMenuRestored(iconSizes[mode]);
        }
        Require(SaveManager.Instance.SettingsSave.Language == nativeLanguage, "Temporary display never changes the native saved language");
        LanguageSwitcher.SwitchTo("fra");
        var french = title.Text;
        await SendKey(Key.F7, true);
        await SendKey(Key.F7, false);
        Require(title.Text == french && LanguagePreferences.Load().Mode is null, "An initial language outside the pair is restored");

        LanguageSwitcher.SwitchTo("eng");
        var editor = new LineEdit();
        game.AddChild(editor);
        editor.GrabFocus();
        await SendKey(Key.F7, true);
        Require(!LanguageSwitcher.IsPeeking && title.Text == "Strike", "Typing ignores the hold shortcut");
        await SendKey(Key.F7, false);
        editor.ReleaseFocus();
        await SendKey(Key.F7, true);
        editor.GrabFocus();
        await SendKey(Key.F7, false);
        Require(!LanguageSwitcher.IsPeeking && title.Text == "Strike", "Release restores even after an editor gained focus");
        editor.ReleaseFocus();
        editor.QueueFree();

        await SendKey(Key.F7, true);
        game.GetWindow().EmitSignal(Window.SignalName.FocusExited);
        await Frames();
        Require(!LanguageSwitcher.IsPeeking && title.Text == "Strike", "Losing window focus restores the original display");
        await SendKey(Key.F7, false);
        game.GetWindow().EmitSignal(Window.SignalName.FocusEntered);
        await SendKey(Key.F7, true);
        await SendKey(Key.F8, true);
        await SendKey(Key.F8, false);
        await SendKey(Key.F7, false);
        Require(!LanguageSwitcher.IsPeeking && LanguagePreferences.Load().Mode == DisplayMode.Second && title.Text == "ストライク", "Explicit F8 mode change wins over a held peek");

        LanguageSwitcher.SwitchTo("eng");
        await SendKey(Key.F7, true);
        await SendKey(Key.F9, true);
        await SendKey(Key.F9, false);
        Require(LanguageSwitcher.Overlay.SettingsVisible && !LanguageSwitcher.IsPeeking && title.Text == "Strike", "Opening F9 restores the base display before changing settings");
        CheckMenuRestored(iconSizes[DisplayMode.First]);
        await Screenshot("settings");
        await SendKey(Key.F7, false);
        await SendKey(Key.F9, true);
        await SendKey(Key.F9, false);

        await SendKey(Key.F7, true);
        LanguageSwitcher.SetPair("fra", "kor");
        await SendKey(Key.F7, false);
        Require(!LanguageSwitcher.IsPeeking && LocManager.Instance.Language == "fra", "Pair changes are not undone by key release");
        await SendKey(Key.F7, true);
        Require(LocManager.Instance.Language == "kor" && title.Text != french, "Mother tongue follows the configured second language");
        await SendKey(Key.F7, false);
        Require(LocManager.Instance.Language == "fra", "Selected learning language restores");

        LanguageSwitcher.SetPair("eng", "jpn");
        LanguageSwitcher.ShowBilingual();
        await SendKey(Key.F7, true);
        game.MainMenu!.OpenSettingsMenu();
        await Frames();
        var dropdown = Descendants(game).OfType<NLanguageDropdown>().First(d => d.IsVisibleInTree());
        var item = Descendants(dropdown).OfType<NLanguageDropdownItem>().First(i => i.LanguageCode == "jpn");
        dropdown.Call("OnDropdownItemSelected", item);
        await Frames();
        await SendKey(Key.F7, false);
        Require(LanguageSwitcher.IsBilingual && !LanguageSwitcher.IsPeeking && LanguageSwitcher.Preferences.First == "jpn"
            && SaveManager.Instance.SettingsSave.Language == "jpn", "Native language selection while peeking keeps the original bilingual mode and saves the explicit choice");

        LanguageSwitcher.SetPair("eng", "jpn");
        LanguageSwitcher.ShowBilingual();
        await SendKey(Key.F7, true);
        Require(LanguageSwitcher.IsPeeking && !LanguageSwitcher.IsBilingual && LanguagePreferences.Load().Mode == DisplayMode.Bilingual,
            "Quitting while held leaves the original bilingual mode saved for the next process");
        ModEntry.Logger.Info("HOLD TRANSLATION PASS: F7 press/hold/release, all 3 modes, no config writes, typing, focus loss, F8/F9, language pairs, native settings, quit while held");
    }

    private static async Task SendKey(Key key, bool down)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = down });
        Input.FlushBufferedEvents();
        await Frames();
    }

    private static void CheckMenuRestored(Vector2 expectedIconSize)
    {
        var menu = NGame.Instance!.MainMenu!;
        foreach (var button in Descendants(menu).OfType<NMainMenuTextButton>().Where(b => b.IsVisibleInTree() && b.GetParent().Name == "MainMenuTextButtons"))
            Require(Math.Abs(button.GetChild<Label>(0).GetGlobalRect().GetCenter().X - button.GetGlobalRect().GetCenter().X) < 1,
                "Single-language menu stays centered after repeated holds: " + button.Name);
        Require(ProfileIcon().Size.IsEqualApprox(expectedIconSize), "Profile icon returns to its native size after release: " + ProfileIcon().Size + " expected " + expectedIconSize);
    }

    private static Control ProfileIcon() => Descendants(NGame.Instance!.MainMenu!).OfType<NOpenProfileScreenButton>().Single().GetNode<Control>("%ProfileIcon");

    private static async Task Frames()
    {
        var game = NGame.Instance!;
        for (var i = 0; i < 3; i++) await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        yield return node;
        foreach (var child in node.GetChildren()) foreach (var item in Descendants(child)) yield return item;
    }

    private static async Task Screenshot(string name)
    {
        if (System.Environment.GetEnvironmentVariable("BILINGUAL_SPIRE_SCREENSHOT") is not { } screenshot) return;
        await Frames();
        await NGame.Instance!.ToSignal(NGame.Instance.GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
        RenderingServer.ForceDraw();
        Require(NGame.Instance!.GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(screenshot)!, "hold-" + name + ".png")) == Error.Ok, "Screenshot saved");
    }

    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
#endif
