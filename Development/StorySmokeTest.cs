#if DEBUG
using BilingualSpire.Localization;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.addons.mega_text;

namespace BilingualSpire.Development;

internal static class StorySmokeTest
{
    internal static async Task Run()
    {
        var game = NGame.Instance!;
        var network = new NetSingleplayerGameService();
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, network.NetId);
        var state = RunState.CreateForTest([player], seed: "BILINGUALSTORY");
        RunManager.Instance.SetUpTest(state, network, shouldSave: false);
        RunManager.Instance.Launch();
        RunManager.Instance.GenerateRooms();
        var run = NRun.Create(state);
        game.RootSceneContainer.SetCurrentScene(run);
        try
        {
            foreach (var canonical in new EventModel[] { ModelDb.Event<SelfHelpBook>(), ModelDb.Event<WaterloggedScriptorium>(), ModelDb.Event<DollRoom>(), ModelDb.Event<Neow>() })
            {
                LanguageSwitcher.SwitchTo("eng");
                var model = canonical.ToMutable();
                await model.BeginEvent(player, null, false);
                var room = NEventRoom.Create(model, state, false)!;
                run.SetCurrentRoom(room);
                try
                {
                    await Settle(model is Neow ? 4.5 : 2.2);
                    var layout = room.Layout!;
                    Require(layout is not null && layout.OptionButtons.Any(), $"{model.Id}: actual event layout initialized");
                    var labels = Descendants(layout!).OfType<Control>().Where(c => c is MegaLabel or MegaRichTextLabel)
                        .Where(c => !string.IsNullOrWhiteSpace(LiveText.GetText(c))).ToArray();
                    var english = labels.ToDictionary(c => c, c => LiveText.GetText(c)!);
                    LanguageSwitcher.SwitchTo("jpn");
                    await Settle(0.1);
                    var japanese = labels.ToDictionary(c => c, c => LiveText.GetText(c)!);
                    var changed = labels.Where(c => english[c] != japanese[c]).ToArray();
                    Require(changed.Length >= 2, $"{model.Id}: real event labels translated");
                    LanguageSwitcher.ShowBilingual();
                    await Settle(0.15);
                    foreach (var label in changed)
                        Require(LiveText.GetText(label) == LiveText.Combine(english[label], japanese[label], label is RichTextLabel), $"{model.Id}/{label.Name}: both complete translations in paragraph order");
                    CheckLayout(model.Id.Entry, layout!);
                    await Screenshot(model.Id.Entry.ToLowerInvariant());
                    if (layout!.GetNodeOrNull<MegaRichTextLabel>("%EventDescription")?.GetParent() is ScrollContainer scroll)
                    {
                        Require(scroll.Size.Y > 120, "Story keeps a readable scroll area");
                        scroll.ScrollVertical = int.MaxValue;
                        await Settle(0.1);
                        var body = scroll.GetChild<Control>(0);
                        Require(scroll.ScrollVertical > 0 || body.Size.Y <= scroll.Size.Y - 16, $"Long story can be read completely: body={body.Size}, scroll={scroll.Size}, offset={scroll.ScrollVertical}");
                        Require(body.GetGlobalRect().End.Y <= scroll.GetGlobalRect().End.Y + 3, "End of translated story is reachable");
                        if (model is DollRoom) await Screenshot("doll_room-translated");
                    }

                    if (layout is NAncientEventLayout ancient)
                    {
                        var dialogueCount = Descendants(ancient).OfType<NAncientDialogueLine>().Count();
                        Require(dialogueCount > 0, "Ancient dialogue lines created");
                        for (var i = 1; i < dialogueCount; i++)
                        {
                            ancient.Call("SetDialogueLineAndAnimate", i);
                            await Settle(1.1);
                            CheckLayout($"{model.Id.Entry}/dialogue-{i}", ancient);
                        }
                        await Screenshot("neow-options");
                    }
                    LanguageSwitcher.SwitchTo("eng");
                    await Settle(0.1);
                    foreach (var label in changed)
                        Require(LiveText.GetText(label) == english[label], $"{model.Id}/{label.Name}: single-language round trip");
                    Require(room.Layout == layout, "Language change kept the event and choice state intact");
                    ModEntry.Logger.Info($"STORY PASS {model.Id.Entry}: {changed.Length} translated labels, {layout!.OptionButtons.Count()} options, layout checked");
                    if (model is DollRoom)
                    {
                        LanguageSwitcher.ShowBilingual();
                        var before = model.Description!.LocEntryKey;
                        await model.CurrentOptions[1].ThatWontSaveToChoiceHistory().Chosen();
                        await Settle(2.2);
                        Require(model.Description!.LocEntryKey != before && layout.OptionButtons.Count() == 2, "Doll Room choice advanced to the next story page");
                        var description = layout.GetNode<MegaRichTextLabel>("%EventDescription");
                        var firstText = model.Description.GetFormattedText();
                        var secondText = ScopedLocale.Render("jpn", model.Description.GetFormattedText);
                        Require(description.Text == LiveText.Combine(firstText, secondText) && description.VisibleRatio == 1, "New story page reveals both complete translations");
                        CheckLayout("DOLL_ROOM/next-page", layout);
                        await Screenshot("doll_room-next-page");
                        ModEntry.Logger.Info("STORY PASS DOLL_ROOM: actual choice advanced the story with bilingual text");
                    }
                    if (model is AncientEventModel ancientModel && layout is NAncientEventLayout dialogueLayout)
                    {
                        var dialogue = ancientModel.DialogueSet.CharacterDialogues[player.Character.Id.Entry].MaxBy(d => d.Lines.Count)!;
                        dialogueLayout.ClearDialogue();
                        foreach (var line in dialogue.Lines) line.LineText?.Add("Act1Name", state.Acts[0].Title);
                        dialogueLayout.SetDialogue(dialogue.Lines);
                        dialogueLayout.OnSetupComplete();
                        for (var i = 0; i < dialogue.Lines.Count; i++)
                        {
                            if (i > 0) dialogueLayout.Call("SetDialogueLineAndAnimate", i);
                            await Settle(1.1);
                            LanguageSwitcher.ShowBilingual();
                            await Settle(0.15);
                            CheckLayout($"NEOW/multi-line-{i}", dialogueLayout);
                            var bubble = dialogueLayout.GetNode<VBoxContainer>("%DialogueContainer").GetChild<Control>(i);
                            var viewport = dialogueLayout.GetNode<Control>("%ContentContainer").GetGlobalRect();
                            await Screenshot($"neow-dialogue-{i}");
                            Require(bubble.GlobalPosition.Y >= viewport.Position.Y - 3 && bubble.GetGlobalRect().End.Y <= viewport.End.Y + 3, $"Current dialogue {i} fits its viewport after changing mode: bubble={bubble.GetGlobalRect()}, viewport={viewport}");
                            LanguageSwitcher.SwitchTo("eng");
                        }
                        ModEntry.Logger.Info($"STORY PASS NEOW: {dialogue.Lines.Count} sequential dialogue lines and live mode changes");
                    }
                }
                finally
                {
                    run.SetCurrentRoom(new Control());
                    model.EnsureCleanup();
                    await Settle(0.05);
                }
            }
        }
        finally
        {
            game.RootSceneContainer.SetCurrentScene(new Control());
            RunManager.Instance.CleanUp();
        }
    }

    private static void CheckLayout(string name, NEventLayout layout)
    {
        foreach (var label in Descendants(layout).OfType<MegaRichTextLabel>().Where(l => l.IsVisibleInTree() && !string.IsNullOrWhiteSpace(l.Text)))
        {
            var height = label.GetContentHeight();
            ModEntry.Logger.Info($"STORY LAYOUT {name}/{label.Name}: content={height}, box={label.Size}, font={label.GetThemeFontSize("normal_font_size")}, fit={label.FitContent}");
            Require(height <= label.Size.Y + 3, $"{name}/{label.Name}: text clipped vertically ({height} > {label.Size.Y})");
            Require(!label.Text.Contains('{'), $"{name}/{label.Name}: unresolved localization variables");
        }
        var buttons = layout.OptionButtons.Select(b => b.GetNode<MegaRichTextLabel>("%Text")).ToArray();
        for (var i = 1; i < buttons.Length; i++)
            Require(!buttons[i - 1].GetGlobalRect().Intersects(buttons[i].GetGlobalRect()), $"{name}: adjacent options overlap");
        if (layout is not NAncientEventLayout)
            foreach (var button in buttons)
                Require(button.GetGlobalRect().End.Y < layout.GetViewportRect().Size.Y, $"{name}: option fell below screen");
    }

    private static async Task Screenshot(string name)
    {
        if (System.Environment.GetEnvironmentVariable("BILINGUAL_SPIRE_SCREENSHOT") is not { } screenshot) return;
        var game = NGame.Instance!;
        RenderingServer.ForceDraw();
        await Task.CompletedTask;
        var path = Path.Combine(Path.GetDirectoryName(screenshot)!, $"story-{name}.png");
        Require(game.GetViewport().GetTexture().GetImage().SavePng(path) == Error.Ok, $"Saved {path}");
    }

    private static async Task Settle(double seconds)
    {
        var game = NGame.Instance!;
        await game.ToSignal(game.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
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
