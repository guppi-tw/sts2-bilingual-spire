#if DEBUG
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.addons.mega_text;

namespace BilingualSpire.Development;

internal static class DemoRecording
{
    internal static async Task Run()
    {
        var output = System.Environment.GetEnvironmentVariable("BILINGUAL_SPIRE_DEMO_TIMING")
            ?? throw new InvalidOperationException("A demo timing output path is required");
        var game = NGame.Instance!;
        var network = new NetSingleplayerGameService();
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, network.NetId);
        var state = RunState.CreateForTest([player], seed: "BILINGUALDEMO");
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
            LanguageSwitcher.SetPair("eng", "jpn");
            LanguageSwitcher.SwitchTo("eng");
            state.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, ModelDb.Event<SelfHelpBook>().Id);
            await manager.EnterRoom(new EventRoom(ModelDb.Event<SelfHelpBook>()));
            await Frames(120);
            var body = NEventRoom.Instance!.Layout!.GetNode<MegaRichTextLabel>("%EventDescription");
            var english = body.Text;
            Require(!string.IsNullOrWhiteSpace(english), "Event is ready");
            var caption = AddCaption(game);
            var start = Engine.GetProcessFrames();
            var markers = new Dictionary<string, ulong> { ["start"] = start };
            Caption(caption, "ふだんは英語で読む");
            await Frames(90);

            await SendKey(Key.Z, true);
            Require(LanguageSwitcher.IsPeeking && body.Text != english, "Z shows the actual translated event");
            markers["hold"] = Engine.GetProcessFrames();
            Caption(caption, "Z を長押し  →  日本語を表示");
            await Frames(105);

            await SendKey(Key.Z, false);
            Require(!LanguageSwitcher.IsPeeking && body.Text == english, "Releasing Z restores the same event");
            markers["release"] = Engine.GetProcessFrames();
            Caption(caption, "Z を離す  →  英語に戻る");
            await Frames(90);

            await SendKey(Key.C, true);
            await SendKey(Key.C, false);
            await SendKey(Key.C, true);
            await SendKey(Key.C, false);
            Require(LanguageSwitcher.IsBilingual, "C cycles to bilingual mode");
            markers["bilingual"] = Engine.GetProcessFrames();
            Caption(caption, "C で常時併記  →  英語 ＋ 日本語");
            await Frames(135);

            var deck = NDeckViewScreen.ShowScreen(player)
                ?? throw new InvalidOperationException("Native deck screen could not open");
            Caption(caption, "カードにも英語と日本語を併記");
            await Frames(45);
            var card = Descendants(deck).OfType<NGridCardHolder>()
                .First(c => c.CardModel.Id.Entry == "BASH" && c.IsVisibleInTree());
            var position = card.Hitbox.GetGlobalRect().GetCenter();
            game.GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
            await Frames(15);
            Require(Descendants(game.HoverTipsContainer!).OfType<Control>()
                .Any(c => c.Name == "BilingualCardTip" && c.IsVisibleInTree()), "The real hover tooltip is visible");
            markers["card"] = Engine.GetProcessFrames();
            Caption(caption, "カードにカーソルを合わせると大きな対訳");
            await Frames(180);
            markers["end"] = Engine.GetProcessFrames();
            File.WriteAllText(output, JsonSerializer.Serialize(new { fps = 30, markers }));
            ModEntry.Logger.Info("DEMO RECORDING PASS: native event, Z press/release, C bilingual mode, native deck and card hover");
        }
        finally
        {
            game.RootSceneContainer.SetCurrentScene(new Control());
            manager.CleanUp();
        }
    }

    private static Label AddCaption(NGame game)
    {
        var layer = new CanvasLayer { Name = "RecordingCaptions", Layer = 110 };
        game.AddChild(layer);
        var label = new Label
        {
            Position = new Vector2(480, 94), Size = new Vector2(960, 68),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontOverride("font", new SystemFont { FontNames = ["Hiragino Sans", "Arial"] });
        label.AddThemeFontSizeOverride("font_size", 30);
        label.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color("15222fee"), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
        });
        layer.AddChild(label);
        return label;
    }

    private static void Caption(Label label, string text)
    {
        NGame.Instance!.GetNode<Control>("BilingualSpire/LanguageControls/LanguageStatus").Hide();
        label.Text = text;
    }

    private static async Task SendKey(Key key, bool down)
    {
        // Synthetic focus/input make this automated recording independent of desktop focus.
        // Display changes still go through the mod's regular keyboard handler.
        NGame.Instance!.GetWindow().EmitSignal(Window.SignalName.FocusEntered);
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = down });
        Input.FlushBufferedEvents();
        await Frames(3);
    }

    private static async Task Frames(int count)
    {
        var tree = NGame.Instance!.GetTree();
        for (var i = 0; i < count; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        yield return node;
        foreach (var child in node.GetChildren()) foreach (var item in Descendants(child)) yield return item;
    }

    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
#endif
