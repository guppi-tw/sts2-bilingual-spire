using System.Runtime.CompilerServices;
using BilingualSpire.Localization;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.addons.mega_text;

namespace BilingualSpire.Patches;

[HarmonyPatch(typeof(NHoverTipSet), "Init")]
internal static class CardHoverPatch
{
    private static readonly ConditionalWeakTable<NHoverTipSet, CardTip> Tips = new();

    private static void Postfix(NHoverTipSet __instance, Control owner, VFlowContainer ____textHoverTipContainer)
    {
        if (owner is not NCardHolder holder) return;
        try
        {
            var tip = new CardTip(__instance, holder, ____textHoverTipContainer);
            Tips.Add(__instance, tip);
            tip.QueueRefresh();
        }
        catch (Exception error) { LiveText.Report(error); }
    }

    internal static void Refresh(NCard? card = null)
    {
        foreach (var (set, tip) in Tips)
            if (GodotObject.IsInstanceValid(set) && !set.IsQueuedForDeletion()
                && (card is null || tip.Holder.CardNode == card)) tip.QueueRefresh();
    }

    internal static bool Scroll(InputEventMouseButton wheel)
    {
        foreach (var (set, tip) in Tips)
            if (GodotObject.IsInstanceValid(set) && !set.IsQueuedForDeletion() && tip.Scroll(wheel)) return true;
        return false;
    }

    private sealed class CardTip
    {
        internal readonly NCardHolder Holder;
        private readonly NHoverTipSet _set;
        private readonly VFlowContainer _flow;
        private readonly Control _panel;
        private readonly MegaLabel _title;
        private readonly MegaRichTextLabel _body;
        private bool _queued;

        internal CardTip(NHoverTipSet set, NCardHolder holder, VFlowContainer flow)
        {
            _set = set;
            Holder = holder;
            _flow = flow;
            _panel = PreloadManager.Cache.GetScene("res://scenes/ui/hover_tip.tscn").Instantiate<Control>();
            _panel.Name = "BilingualCardTip";
            _panel.Visible = false;
            flow.AddChild(_panel);
            flow.MoveChild(_panel, 0);
            _title = _panel.GetNode<MegaLabel>("%Title");
            _body = _panel.GetNode<MegaRichTextLabel>("%Description");
            _body.Name = "BilingualCardDescription";
            _title.AutoSizeEnabled = _body.AutoSizeEnabled = false;
            _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _title.HorizontalAlignment = HorizontalAlignment.Left;
            _title.AddThemeFontSizeOverride("font_size", 26);
            _body.AddThemeFontSizeOverride("normal_font_size", 26);
            _body.CustomMinimumSize = new Vector2(460, 0);
        }

        private bool Alive => GodotObject.IsInstanceValid(_set) && !_set.IsQueuedForDeletion()
            && GodotObject.IsInstanceValid(Holder) && !Holder.IsQueuedForDeletion();

        internal bool Scroll(InputEventMouseButton wheel)
        {
            if (!Alive || !_panel.Visible || _body.GetContentHeight() <= _body.Size.Y
                || !new Rect2(Vector2.Zero, Holder.Hitbox.Size).HasPoint(
                    Holder.Hitbox.GetGlobalTransformWithCanvas().AffineInverse() * wheel.Position)) return false;
            _body.GetVScrollBar().Value += (wheel.ButtonIndex == MouseButton.WheelDown ? 1 : -1) * 78 * Math.Max(1, wheel.Factor);
            return true;
        }

        internal void QueueRefresh()
        {
            if (_queued) return;
            _queued = true;
            Callable.From(Update).CallDeferred();
        }

        private void Update()
        {
            _queued = false;
            if (!Alive) return;
            try
            {
                var card = Holder.CardNode;
                _panel.Visible = LanguageSwitcher.IsBilingual && card is not null
                    && card.Visibility == ModelVisibility.Visible && card.Model is not null;
                if (_panel.Visible)
                {
                    LiveText.BindRenderer(_title, () => card!.Model!.Title);
                    LiveText.BindRenderer(_body, () => CardRefreshPatch.Description(card!));
                    _body.FitContent = true;
                    _body.CustomMinimumSize = new Vector2(460, 0);
                    _panel.ResetSize();
                }
                Callable.From(Reflow).CallDeferred();
            }
            catch (Exception error) { LiveText.Report(error); }
        }

        private void Reflow()
        {
            if (!Alive) return;
            // Preserve the native tooltip skin, keyword tips and hover lifetime. Very long
            // descriptions scroll at the same readable size instead of shrinking the text.
            var available = _set.GetViewportRect().Size.Y - 48;
            if (_panel.Visible)
            {
                var height = Math.Min(_body.GetContentHeight(), Math.Min(600, available - 150));
                _body.FitContent = false;
                _body.ScrollActive = true;
                _body.CustomMinimumSize = new Vector2(460, height);
                _panel.ResetSize();
            }
            Callable.From(() =>
            {
                if (!Alive) return;
                var children = _flow.GetChildren().OfType<Control>().Where(c => c.Visible).ToArray();
                _flow.Size = new Vector2(children.Select(c => c.GetCombinedMinimumSize().X).DefaultIfEmpty(360).Max(),
                    Math.Min(available, children.Sum(c => c.Size.Y + 5)));
                Callable.From(() => { if (Alive) _set.SetAlignmentForCardHolder(Holder); }).CallDeferred();
            }).CallDeferred();
        }
    }
}

[HarmonyPatch(typeof(NGame), nameof(NGame._Input))]
internal static class CardHoverScrollPatch
{
    private static void Postfix(NGame __instance, InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown or MouseButton.WheelUp } wheel) return;
        if (CardHoverPatch.Scroll(wheel)) __instance.GetViewport().SetInputAsHandled();
    }
}
