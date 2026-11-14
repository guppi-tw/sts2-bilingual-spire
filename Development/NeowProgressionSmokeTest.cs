#if DEBUG
using BilingualSpire.Settings;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;

namespace BilingualSpire.Development;

internal static class NeowProgressionSmokeTest
{
    internal static async Task Run()
    {
        var game = NGame.Instance!;
        var network = new NetSingleplayerGameService();
        var player = Player.CreateForNewRun<Fool>(UnlockState.all, network.NetId);
        var state = RunState.CreateForTest([player], seed: "FYKHA5QLX853");
        var manager = RunManager.Instance;
        manager.SetUpTest(state, network, shouldSave: false);
        manager.Launch();
        manager.GenerateRooms();
        await PreloadManager.LoadRunAssets([player.Character]);
        await PreloadManager.LoadActAssets(state.Act);
        var run = NRun.Create(state);
        game.RootSceneContainer.SetCurrentScene(run);
        try
        {
            await manager.GenerateMap();
            SaveManager.Instance.MarkFtueAsComplete("obtain_relic_ftue");
            LanguageSwitcher.SetPair("eng", "jpn");
            LanguageSwitcher.ShowBilingual();
            state.AppendToMapPointHistory(MapPointType.Ancient, RoomType.Event, ModelDb.Event<Neow>().Id);
            await manager.EnterRoom(new EventRoom(ModelDb.Event<Neow>())
            {
                OnStart = model => ((Neow)model).DebugOption = "SMALL_CAPSULE"
            });
            await Settle(4.5);
            var room = NEventRoom.Instance!;
            var layout = (NAncientEventLayout)room.Layout!;
            var dialogueCount = layout.GetNode<VBoxContainer>("%DialogueContainer").GetChildCount();
            if (dialogueCount > 1) layout.Call("SetDialogueLineAndAnimate", dialogueCount - 1);
            await Settle(1.1);
            var choice = layout.OptionButtons.First(b => b.Option.Relic?.Id.Entry == "SMALL_CAPSULE");
            CheckVisible(choice, layout, "Small Capsule choice");
            choice.Call("OnRelease");
            await WaitUntil(() => Descendants(game).OfType<NRewardButton>().Any(b => b.IsVisibleInTree()), "relic reward opens");
            var reward = Descendants(game).OfType<NRewardButton>().First(b => b.IsVisibleInTree());
            await RelicTitleSmokeTest.Run(reward);
            reward.Call("OnRelease");
            await WaitUntil(() => layout.OptionButtons.Any(b => b.Option.IsProceed), "Neow completes after collecting the relic");
            await Settle(1.1);
            Require(player.Relics.Any(r => r.Id.Entry == "SMALL_CAPSULE") && player.Relics.Count >= 3, "Both relics were collected");
            Require(layout.GetNode<VBoxContainer>("%DialogueContainer").GetChildCount() == 0, "Completed event cleared its dialogue");
            for (var mode = 0; mode < 4; mode++)
            {
                var expected = mode % 3 == 0 ? DisplayMode.Bilingual : mode == 1 ? DisplayMode.First : DisplayMode.Second;
                Require(LanguagePreferences.Load().Mode == expected, $"F8 reached mode {expected}");
                var proceed = layout.OptionButtons.Single(b => b.Option.IsProceed);
                await Screenshot($"neow-completed-{mode}");
                CheckVisible(proceed, layout, $"Proceed after relic reward, mode {mode}");
                if (mode < 3) await PressF8();
            }
            layout.OptionButtons.Single(b => b.Option.IsProceed).Call("OnRelease");
            await Settle(0.6);
            Require(run.GlobalUi.MapScreen.IsOpen && run.GlobalUi.MapScreen.IsTravelEnabled, "Proceed opens the map and enables travel");
            await Screenshot("neow-map");
            await MapLegendSmokeTest.Run(run.GlobalUi.MapScreen);
            run.GlobalUi.MapScreen.Close(animateOut: false);
            var resumedRoom = new EventRoom(ModelDb.Event<Neow>());
            resumedRoom.MarkPreFinished();
            await manager.EnterRoom(resumedRoom);
            await Settle(1.5);
            var resumedLayout = (NAncientEventLayout)NEventRoom.Instance!.Layout!;
            var resumedProceed = resumedLayout.OptionButtons.Single(b => b.Option.IsProceed);
            CheckVisible(resumedProceed, resumedLayout, "Proceed after resuming a completed Neow event");
            resumedProceed.Call("OnRelease");
            await Settle(0.6);
            Require(run.GlobalUi.MapScreen.IsOpen && run.GlobalUi.MapScreen.IsTravelEnabled, "Resumed event also opens the map");
            ModEntry.Logger.Info("NEOW PROGRESSION PASS: Small Capsule reward, both relics, visible Proceed through the F8 cycle, map opened, completed event resumed");
        }
        finally
        {
            game.RootSceneContainer.SetCurrentScene(new Control());
            manager.CleanUp();
        }
    }

    private static void CheckVisible(Control button, NAncientEventLayout layout, string step)
    {
        var bounds = button.GetGlobalRect();
        var viewport = layout.GetNode<Control>("%ContentContainer").GetGlobalRect();
        var content = layout.GetNode<Control>("%Content");
        ModEntry.Logger.Info($"NEOW GEOMETRY {step}: button={bounds}, viewport={viewport}, content={content.GetGlobalRect()}");
        Require(button.IsVisibleInTree() && button.Modulate.A > 0.9f && button.MouseFilter != Control.MouseFilterEnum.Ignore, $"{step}: visible and enabled");
        Require(bounds.Position.Y >= viewport.Position.Y - 3 && bounds.End.Y <= viewport.End.Y + 3, $"{step}: button clipped outside event viewport");
        Require(layout.GetViewportRect().Encloses(bounds), $"{step}: button outside screen");
    }

    private static async Task WaitUntil(Func<bool> condition, string step)
    {
        for (var i = 0; i < 100; i++)
        {
            if (condition()) return;
            await Settle(0.05);
        }
        throw new InvalidOperationException($"Timed out: {step}");
    }

    private static async Task Settle(double seconds)
    {
        var game = NGame.Instance!;
        await game.ToSignal(game.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }

    private static async Task PressF8()
    {
        var tree = NGame.Instance!.GetTree();
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.F8, Keycode = Key.F8, Pressed = true });
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.F8, Keycode = Key.F8, Pressed = false });
        await Settle(0.15);
    }

    private static async Task Screenshot(string name)
    {
        if (System.Environment.GetEnvironmentVariable("BILINGUAL_SPIRE_SCREENSHOT") is not { } screenshot) return;
        RenderingServer.ForceDraw();
        await Task.CompletedTask;
        var path = Path.Combine(Path.GetDirectoryName(screenshot)!, $"story-{name}.png");
        Require(NGame.Instance!.GetViewport().GetTexture().GetImage().SavePng(path) == Error.Ok, $"Saved {path}");
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        yield return node;
        foreach (var child in node.GetChildren()) foreach (var descendant in Descendants(child)) yield return descendant;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
