using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.addons.mega_text;

namespace BilingualSpire.Patches;

[HarmonyPatch]
internal static class EventLayoutPatch
{
    private sealed class State
    {
        internal ScrollContainer? Scroll;
        internal MegaRichTextLabel? Description;
        internal VBoxContainer? Column;
        internal Vector2 OriginalSize;
        internal Vector2 OriginalMinimum;
        internal Control.SizeFlags OriginalFlags;
        internal int OriginalIndex;
        internal bool Pending;
        internal bool WasBilingual;
    }
    private sealed record OptionSize(Vector2 Minimum, Control TextArea, float BottomAnchor, float BottomOffset);
    private static readonly ConditionalWeakTable<NEventLayout, State> Layouts = new();
    private static readonly ConditionalWeakTable<NEventOptionButton, OptionSize> Options = new();

    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NEventLayout), nameof(NEventLayout.SetDescription));
        yield return AccessTools.Method(typeof(NEventLayout), nameof(NEventLayout.AddOptions));
    }

    private static void Postfix(NEventLayout __instance) => Schedule(__instance);

    internal static void Refresh()
    {
        foreach (var (layout, _) in Layouts.ToArray())
            if (GodotObject.IsInstanceValid(layout) && !layout.IsQueuedForDeletion()) Schedule(layout);
    }

    private static void Schedule(NEventLayout layout)
    {
        var state = Layouts.GetOrCreateValue(layout);
        if (state.Pending) return;
        state.Pending = true;
        Callable.From(() =>
        {
            state.Pending = false;
            if (!GodotObject.IsInstanceValid(layout) || !layout.IsInsideTree() || layout.IsQueuedForDeletion()) return;
            try { Apply(layout, state); }
            catch (Exception error) { Localization.LiveText.Report(error); }
        }).CallDeferred();
    }

    private static void Apply(NEventLayout layout, State state)
    {
        var buttons = layout.OptionButtons.ToArray();
        var bilingual = LanguageSwitcher.IsBilingual;
        foreach (var button in buttons)
        {
            var label = button.GetNode<MegaRichTextLabel>("%Text");
            var textArea = label.GetParent() is HBoxContainer row ? (Control)row : label;
            var original = Options.GetValue(button, b => new(b.CustomMinimumSize, textArea, textArea.AnchorBottom, textArea.OffsetBottom));
            button.CustomMinimumSize = bilingual
                ? new Vector2(original.Minimum.X, buttons.Length <= 3 ? 156 : 124)
                : original.Minimum;
            original.TextArea.AnchorBottom = bilingual ? 1 : original.BottomAnchor;
            original.TextArea.OffsetBottom = bilingual ? -13 : original.BottomOffset;
        }
        if (layout is NAncientEventLayout ancient)
        {
            if (bilingual || state.WasBilingual) RepositionAfterLayout(ancient);
            state.WasBilingual = bilingual;
            return;
        }
        var description = layout.GetNodeOrNull<MegaRichTextLabel>("%EventDescription");
        if (description is null) return;
        if (bilingual)
        {
            if (state.Scroll is null)
            {
                if (description.GetParent() is not VBoxContainer column) return;
                state.Column = column;
                state.Description = description;
                state.OriginalSize = column.Size;
                state.OriginalMinimum = description.CustomMinimumSize;
                state.OriginalFlags = description.SizeFlagsHorizontal;
                state.OriginalIndex = description.GetIndex();
                state.Scroll = new ScrollContainer
                {
                    Name = "BilingualStoryScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                    VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
                    SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    FollowFocus = true
                };
                state.Scroll.AddThemeStyleboxOverride("panel", UI.ModStyles.Panel());
                column.AddChild(state.Scroll);
                column.MoveChild(state.Scroll, state.OriginalIndex);
                description.Reparent(state.Scroll);
                description.CustomMinimumSize = Vector2.Zero;
                description.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                description.MouseFilter = Control.MouseFilterEnum.Pass;
            }
            var available = layout.GetViewportRect().Size.Y - state.Column!.GlobalPosition.Y - 28;
            state.Column.Size = new Vector2(state.OriginalSize.X, available);
            state.Scroll.ScrollVertical = 0;
        }
        else if (state.Scroll is not null)
        {
            description.Reparent(state.Column!);
            state.Column!.MoveChild(description, state.OriginalIndex);
            description.CustomMinimumSize = state.OriginalMinimum;
            description.SizeFlagsHorizontal = state.OriginalFlags;
            state.Column.RemoveChild(state.Scroll);
            state.Scroll.QueueFree();
            state.Scroll = null;
            state.Column.Size = state.OriginalSize;
        }
    }

    private static async void RepositionAfterLayout(NAncientEventLayout layout)
    {
        var tree = layout.GetTree();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        try { RepositionDialogue(layout); }
        catch (Exception error) { Localization.LiveText.Report(error); }
    }

    private static void RepositionDialogue(NAncientEventLayout layout)
    {
        if (!GodotObject.IsInstanceValid(layout) || !layout.IsInsideTree() || layout.IsQueuedForDeletion()) return;
        var content = layout.GetNode<VBoxContainer>("%Content");
        var viewport = layout.GetNode<Control>("%ContentContainer");
        var dialogue = layout.GetNode<VBoxContainer>("%DialogueContainer");
        var options = layout.GetNode<VBoxContainer>("%OptionsContainer");
        var index = (int)AccessTools.Field(typeof(NAncientEventLayout), "_currentDialogueLine").GetValue(layout)!;
        var line = dialogue.GetChildOrNull<Control>(index);
        var height = line is null ? dialogue.Size.Y : line.Position.Y + line.Size.Y;
        if (index >= dialogue.GetChildCount() - 1) height += options.Size.Y + 10;
        var tweenField = AccessTools.Field(typeof(NAncientEventLayout), "_contentTween");
        if (tweenField.GetValue(layout) is Tween tween)
        {
            // Complete the game's focus callback before replacing its now-stale target position.
            if (tween.IsRunning()) tween.CustomStep(1.0);
            tween.Kill();
            tweenField.SetValue(layout, null);
        }
        content.Position = new Vector2(content.Position.X, viewport.Size.Y - height);
    }
}
