#if DEBUG
using BilingualSpire.Settings;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.addons.mega_text;

namespace BilingualSpire.Development;

internal static class PersistenceSmokeTest
{
    internal static async Task Run(string phase)
    {
        var expected = phase switch
        {
            "resume-bilingual" => DisplayMode.Bilingual,
            "resume-first" => DisplayMode.First,
            "resume-second" => DisplayMode.Second,
            _ => throw new ArgumentException($"Unknown test phase: {phase}")
        };
        Require(LanguagePreferences.Load().Mode == expected, "Saved mode loaded on a fresh game process");
        Require(SaveManager.Instance.SettingsSave.Language == "fra", "Every restart begins with an unrelated native language");
        Require(LanguageSwitcher.IsBilingual == (expected == DisplayMode.Bilingual), "Bilingual state restored");
        Require(LocManager.Instance.Language == (expected == DisplayMode.Second ? "jpn" : "eng"), "Base language restored");
        var game = NGame.Instance!;
        var view = NCard.Create(ModelDb.Card<StrikeIronclad>().ToMutable())!;
        game.AddChild(view);
        view.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        var title = view.GetNode<MegaLabel>("%TitleLabel").Text;
        Require(title == (expected == DisplayMode.Bilingual ? "Strike\nストライク" : expected == DisplayMode.First ? "Strike" : "ストライク"), $"New card uses restored display mode: {title}");
        var next = expected switch { DisplayMode.Bilingual => DisplayMode.First, DisplayMode.First => DisplayMode.Second, _ => DisplayMode.Bilingual };
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.C, Keycode = Key.C, Pressed = true });
        await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
        await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.C, Keycode = Key.C, Pressed = false });
        Require(LanguagePreferences.Load().Mode == next, "C saved the next mode for another process");
        Require(LanguageSwitcher.IsBilingual == (next == DisplayMode.Bilingual), "C changed the live display mode");
        ModEntry.Logger.Info($"SMOKE TEST PASS: {phase} restored {expected} in a fresh process, card title={title.Replace('\n', '/')}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
