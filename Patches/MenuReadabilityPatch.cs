using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace BilingualSpire.Patches;

internal static class MenuReadabilityPatch
{
    private sealed record Geometry(Control Control, Vector2 Minimum, Vector2 Size, Vector2 Position, Vector2 Pivot,
        TextServer.AutowrapMode? Wrap, Control.SizeFlags Vertical)
    {
        internal static Geometry Capture(Control c) => new(c, c.CustomMinimumSize, c.Size, c.Position, c.PivotOffset,
            c is Label label ? label.AutowrapMode : null, c.SizeFlagsVertical);
        internal void Restore()
        {
            if (!GodotObject.IsInstanceValid(Control)) return;
            Control.CustomMinimumSize = Minimum;
            if (Control is Label label && Wrap is { } wrap) label.AutowrapMode = wrap;
            Control.Size = Size;
            Control.Position = Position;
            Control.PivotOffset = Pivot;
            Control.SizeFlagsVertical = Vertical;
        }
    }
    private static readonly ConditionalWeakTable<Control, List<Geometry>> Originals = new();
    private static readonly HashSet<Control> Queued = new();
    private static readonly ConditionalWeakTable<Label, object> Watched = new();

    internal static bool IsMenuLabel(Control c) => c is Label && c.GetParent() is NMainMenuTextButton button
        && button.GetParent()?.Name == "MainMenuTextButtons";

    private static NOpenProfileScreenButton? Profile(Control c)
    {
        if (c is not Label || (c.Name != "Title" && c.Name != "Description")) return null;
        for (var p = c.GetParent(); p is not null; p = p.GetParent())
            if (p is NOpenProfileScreenButton profile) return profile;
        return null;
    }

    internal static bool IsManaged(Control c) => IsMenuLabel(c) || Profile(c) is not null;

    internal static void QueueRefresh(Control c)
    {
        var owner = IsMenuLabel(c) ? (Control)c.GetParent() : Profile(c);
        if (owner is null || !Queued.Add(owner)) return;
        Callable.From(() =>
        {
            Queued.Remove(owner);
            if (!GodotObject.IsInstanceValid(owner) || owner.IsQueuedForDeletion()) return;
            try { Apply(owner); }
            catch (Exception error) { Localization.LiveText.Report(error); }
        }).CallDeferred();
    }

    private static void Apply(Control owner)
    {
        if (!LanguageSwitcher.IsBilingual)
        {
            if (Originals.TryGetValue(owner, out var originals))
            {
                foreach (var original in originals.AsEnumerable().Reverse()) original.Restore();
                Originals.Remove(owner);
            }
            return;
        }
        if (owner is NMainMenuTextButton menu)
        {
            var label = menu.GetChild<Label>(0);
            if (!Watched.TryGetValue(label, out _))
            {
                Watched.Add(label, new object());
                label.Resized += () => { if (LanguageSwitcher.IsBilingual) FitButton(owner, label); };
                label.MinimumSizeChanged += () => Callable.From(() =>
                {
                    if (!GodotObject.IsInstanceValid(label) || label.IsQueuedForDeletion() || !LanguageSwitcher.IsBilingual) return;
                    label.ResetSize();
                    FitButton(owner, label);
                }).CallDeferred();
            }
            if (!Originals.TryGetValue(owner, out _)) Originals.Add(owner, [Geometry.Capture(owner), Geometry.Capture(label)]);
            var width = Math.Min(600, label.GetThemeFont("font").GetStringSize(label.Text, fontSize: label.GetThemeFontSize("font_size")).X + 4);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(width, 0);
            label.Size = new Vector2(width, label.GetMinimumSize().Y);
            FitButton(owner, label);
        }
        else
        {
            var box = owner.GetNode<HBoxContainer>("HBoxContainer");
            var title = owner.GetNode<Label>("%Title");
            var description = owner.GetNode<Label>("%Description");
            var icon = owner.GetNode<Control>("%ProfileIcon");
            if (!Originals.TryGetValue(owner, out _)) Originals.Add(owner,
                [Geometry.Capture(owner), Geometry.Capture(box), Geometry.Capture(title), Geometry.Capture(description), Geometry.Capture(icon)]);
            icon.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            foreach (var label in new[] { title, description })
            {
                label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                label.CustomMinimumSize = new Vector2(340, 0);
            }
            box.ResetSize();
            Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(owner) || owner.IsQueuedForDeletion() || !LanguageSwitcher.IsBilingual) return;
                box.ResetSize();
                owner.Size = box.GetCombinedMinimumSize() + new Vector2(16, 16);
                owner.PivotOffset = owner.Size / 2;
            }).CallDeferred();
        }
    }

    private static void FitButton(Control owner, Label label)
    {
        owner.CustomMinimumSize = new Vector2(Math.Max(200, label.Size.X + 32), Math.Max(50, label.Size.Y + 8));
        label.OffsetLeft = -label.Size.X / 2;
        label.OffsetRight = label.Size.X / 2;
        label.OffsetTop = -label.Size.Y / 2;
        label.OffsetBottom = label.Size.Y / 2;
    }
}
