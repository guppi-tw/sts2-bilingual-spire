#if DEBUG
using BilingualSpire.Localization;
using BilingualSpire.Patches;
using BilingualSpire.Settings;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.addons.mega_text;

namespace BilingualSpire.Development;

internal static class ReadabilitySmokeTest
{
    internal static async Task Run()
    {
        var game = NGame.Instance!;
        LanguageSwitcher.SetPair("eng", "jpn");
        LanguageSwitcher.SwitchTo("eng");
        await Settle();
        var profile = Descendants(game.MainMenu!).OfType<NOpenProfileScreenButton>().Single();
        var profileSize = profile.Size;
        var buttons = Descendants(game.MainMenu!).OfType<NMainMenuTextButton>()
            .Where(b => b.GetParent().Name == "MainMenuTextButtons").ToArray();
        var minima = buttons.ToDictionary(b => b, b => b.CustomMinimumSize);
        var pairs = LanguagePreferences.Languages.Where(l => l != "eng")
            .SelectMany(l => new[] { (First: "eng", Second: l), (First: l, Second: "eng") });
        foreach (var pair in pairs)
        {
            LanguageSwitcher.SetPair(pair.First, pair.Second);
            LanguageSwitcher.ShowBilingual();
            await Settle();
            foreach (var button in buttons.Where(b => b.IsVisibleInTree()))
            {
                var label = button.GetChild<MegaLabel>(0);
                Require(label.GetThemeFontSize("font_size") == 32, "Menu preserves native 32px: " + label.Text);
                Require(!label.Text.Contains('\n'), "Menu uses inline translations");
                Require(button.GetGlobalRect().Grow(1).Encloses(label.GetGlobalRect()), "Menu hitbox covers label: " + label.GetGlobalRect() + " in " + button.GetGlobalRect());
                Require(label.GetViewportRect().Encloses(label.GetGlobalRect()), "Menu stays on screen");
            }
            foreach (var label in new[] { profile.GetNode<Label>("%Title"), profile.GetNode<Label>("%Description") })
            {
                Require(label.GetThemeFontSize("font_size") == (label.Name == "Title" ? 24 : 18), "Profile preserves native font size");
                Require(profile.GetGlobalRect().Grow(1).Encloses(label.GetGlobalRect()), "Profile hitbox covers text");
                Require(label.Size.Y + 1 >= label.GetMinimumSize().Y, "Profile has space for wrapped lines");
            }
        }
        LanguageSwitcher.SetPair("eng", "jpn");
        await Settle();
        var singleplayer = buttons.Single(b => b.Name == "SingleplayerButton");
        singleplayer.Call("OnFocus");
        await Settle();
        await Screenshot("menu");
        singleplayer.Call("OnUnfocus");
        LanguageSwitcher.SwitchTo("eng");
        await Settle();
        Require(profile.Size.IsEqualApprox(profileSize), "Profile size restores: " + profile.Size + " expected " + profileSize);
        Require(buttons.All(b => b.CustomMinimumSize == minima[b]), "Menu button sizes restore");

        game.MainMenu!.Hide();
        var model = ModelDb.AllCards.First(c => c.Id.Entry == "CORRUPTION").ToMutable();
        var view = NCard.Create(model)!;
        var holder = NGridCardHolder.Create(view)!;
        game.AddChild(holder);
        holder.Position = new Vector2(420, 550);
        view.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        LanguageSwitcher.ShowBilingual();
        await Settle();
        MoveMouse(holder.Hitbox.GetGlobalRect().GetCenter());
        await Settle();
        CheckCard(holder);
        await Screenshot("card-left");
        Require(Descendants(game.HoverTipsContainer!).OfType<MegaLabel>().Count(l => l.Name == "Title") > 1,
            "Existing keyword hover tips remain alongside the full description");
        var longBody = Descendants(Panel()).OfType<MegaRichTextLabel>().Single();
        LiveText.BindRenderer(longBody, () => string.Join("\n\n", Enumerable.Repeat(CardRefreshPatch.Description(view), 20)));
        await Settle();
        Require(longBody.GetContentHeight() > longBody.Size.Y, "Long-text fixture overflows the readable panel");
        var mousePosition = holder.Hitbox.GetGlobalRect().GetCenter();
        game.GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = true,
            Position = mousePosition, GlobalPosition = mousePosition }, true);
        await Settle();
        Require(longBody.GetVScrollBar().Value > 0, "Wheel over the card scrolls its large description");
        holder.ReassignToCard(ModelDb.AllCards.First(c => c.Id.Entry == "BASH").ToMutable(), PileType.None, null, ModelVisibility.Visible);
        await Settle();
        CheckCard(holder);
        var normalDescription = Descendants(Panel()).OfType<MegaRichTextLabel>().Single().Text;
        holder.SetIsPreviewingUpgrade(true);
        await Settle();
        CheckCard(holder);
        Require(Descendants(Panel()).OfType<MegaRichTextLabel>().Single().Text != normalDescription, "Upgraded preview updates displayed numbers");
        holder.SetIsPreviewingUpgrade(false);
        await Settle();
        CheckCard(holder);
        LanguageSwitcher.SwitchTo("eng");
        await Settle();
        Require(!Panel().Visible, "Single-language mode hides extra card tip while hovered");
        LanguageSwitcher.ShowBilingual();
        await Settle();
        CheckCard(holder);
        MoveMouse(new Vector2(10, 500));
        await Settle();
        Require(!Descendants(game.HoverTipsContainer!).Any(n => n.Name == "BilingualCardTip"), "Hover-out removes the full tip");

        holder.Position = new Vector2(1570, 780);
        holder.ReassignToCard(ModelDb.AllCards.First(c => c.Id.Entry == "ASCENDERS_BANE").ToMutable(), PileType.None, null, ModelVisibility.Visible);
        holder.Hitbox.EmitSignal("Focused", holder.Hitbox);
        await Settle();
        CheckCard(holder);
        await Screenshot("card-right");
        LanguageSwitcher.SetPair("fra", "kor");
        await Settle();
        CheckCard(holder);
        await Screenshot("card-french-korean");
        holder.ReassignToCard(model, PileType.None, null, ModelVisibility.Locked);
        await Settle();
        Require(!Panel().Visible, "A hidden/locked card never reveals a full description");
        holder.Hitbox.EmitSignal("Unfocused", holder.Hitbox);
        holder.QueueFree();
        game.MainMenu.Show();
        LanguageSwitcher.SetPair("eng", "jpn");
        LanguageSwitcher.ShowBilingual();
        ModEntry.Logger.Info("READABILITY PASS: all 16 menu languages in both positions, native font sizes and hitboxes, restoration; mouse card hover, long-text wheel scrolling, edges, upgraded numbers, mode changes, French/Korean, hover-out, locked cards");
    }

    private static Control Panel() => Descendants(NGame.Instance!.HoverTipsContainer!).OfType<Control>().Single(n => n.Name == "BilingualCardTip");

    private static void MoveMouse(Vector2 position) => NGame.Instance!.GetViewport().PushInput(
        new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);

    private static void CheckCard(NCardHolder holder)
    {
        var panel = Panel();
        var body = Descendants(panel).OfType<MegaRichTextLabel>().Single();
        var title = Descendants(panel).OfType<MegaLabel>().Single();
        Require(panel.Visible, "Full card tip visible");
        Require(body.GetThemeFontSize("normal_font_size") == 26 && body.Text.Contains("[font_size=22]"), "Readable primary and 85% secondary fonts");
        var first = ScopedLocale.Render(LanguageSwitcher.Preferences.First, () => CardRefreshPatch.Description(holder.CardNode!));
        var second = ScopedLocale.Render(LanguageSwitcher.Preferences.Second, () => CardRefreshPatch.Description(holder.CardNode!));
        Require(body.Text == LiveText.CombineFor(body, first, second), "Full tip matches current preview in both languages");
        Require(holder.CardNode!.GetNode<MegaRichTextLabel>("%DescriptionLabel").Text.Contains("[font_size="), "Card itself keeps both languages");
        Require(body.GetViewportRect().Grow(1).Encloses(body.GetGlobalRect()) && title.GetViewportRect().Grow(1).Encloses(title.GetGlobalRect()),
            "Large card text fits viewport: " + body.GetGlobalRect() + " title=" + title.GetGlobalRect());
        Require(panel.GetNode<Control>("%Bg").GetGlobalRect().Grow(1).Encloses(body.GetGlobalRect()), "Native background contains card text");
        ModEntry.Logger.Info($"READABILITY card: {holder.CardNode.Model!.Id} font={body.GetThemeFontSize("normal_font_size")} content={body.GetContentHeight()} box={body.GetGlobalRect()}");
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        yield return node;
        foreach (var child in node.GetChildren()) foreach (var item in Descendants(child)) yield return item;
    }
    private static async Task Settle() => await NGame.Instance!.ToSignal(NGame.Instance.GetTree().CreateTimer(.18), SceneTreeTimer.SignalName.Timeout);
    private static async Task Screenshot(string name)
    {
        if (System.Environment.GetEnvironmentVariable("BILINGUAL_SPIRE_SCREENSHOT") is not { } screenshot) return;
        await Settle();
        RenderingServer.ForceDraw();
        Require(NGame.Instance!.GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(screenshot)!, "readability-" + name + ".png")) == Error.Ok, "Screenshot saved");
    }
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
#endif
