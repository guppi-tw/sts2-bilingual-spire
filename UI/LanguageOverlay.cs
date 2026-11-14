using BilingualSpire.Settings;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace BilingualSpire.UI;

internal sealed class LanguageOverlay
{
    private PanelContainer _panel = null!;
    private Label _status = null!;
    private OptionButton _first = null!;
    private OptionButton _second = null!;
    private string[] _languages = [];
    private int _statusVersion;
    internal bool SettingsVisible => _panel.Visible;
    internal int LanguageCount => _languages.Length;

    internal static string LanguageName(string code) => NLanguageDropdown.GetLanguageNameForCode(code);

    internal void Attach(NGame game)
    {
        var layer = new CanvasLayer { Name = "BilingualSpire", Layer = 100 };
        game.AddChild(layer);
        var root = new Control { Name = "LanguageControls", MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var font = new SystemFont { FontNames = ["Hiragino Sans", "Arial"], AllowSystemFallback = true };
        var theme = new Theme { DefaultFont = font, DefaultFontSize = 18 };
        var surface = ModStyles.Surface;
        foreach (var type in new[] { "Button", "OptionButton" })
        {
            theme.SetStylebox("normal", type, ModStyles.Panel(surface.Lightened(0.08f)));
            theme.SetStylebox("hover", type, ModStyles.Panel(surface.Lightened(0.2f)));
            theme.SetStylebox("pressed", type, ModStyles.Panel(surface.Darkened(0.15f)));
            theme.SetColor("font_color", type, new Color("f4ede0"));
        }
        theme.SetStylebox("panel", "PopupMenu", ModStyles.Panel(surface));
        root.Theme = theme;

        _panel = new PanelContainer { Name = "LanguageSettings", Visible = false };
        _panel.AddThemeStyleboxOverride("panel", ModStyles.Panel(surface));
        root.AddChild(_panel);
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _panel.OffsetLeft = -456;
        _panel.OffsetRight = -20;
        _panel.OffsetTop = 68;
        _panel.OffsetBottom = 340;
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 12);
        _panel.AddChild(rows);
        rows.AddChild(new Label { Text = "切り替える言語", MouseFilter = Control.MouseFilterEnum.Ignore });
        _languages = LanguagePreferences.Languages;
        _first = AddLanguageRow(rows, "言語 1");
        _second = AddLanguageRow(rows, "言語 2");
        rows.AddChild(new Label { Text = "F7  押している間だけ言語2（母国語）を表示\n離すと元の表示に戻ります\nF8  言語1 → 言語2 → 両方表示\nF9  この設定を開閉\n表示モードと言語は次回起動にも引き継ぎます\n本体の言語変更は「言語1」に反映します\n反応しない場合は fn キーも押してください", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var actions = new HBoxContainer();
        rows.AddChild(actions);
        var toggle = new Button { Text = "今すぐ切り替え", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var close = new Button { Text = "閉じる" };
        actions.AddChild(toggle);
        actions.AddChild(close);
        toggle.Pressed += LanguageSwitcher.Toggle;
        close.Pressed += () => _panel.Hide();
        _first.ItemSelected += index => Select(first: true, (int)index);
        _second.ItemSelected += index => Select(first: false, (int)index);
        RefreshSelection();

        _status = new Label { Name = "LanguageStatus", Visible = false, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        _status.AddThemeStyleboxOverride("normal", ModStyles.Panel(surface));
        root.AddChild(_status);
        _status.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _status.OffsetLeft = -320;
        _status.OffsetRight = 320;
        _status.OffsetTop = 72;
        _status.OffsetBottom = 112;
    }

    private OptionButton AddLanguageRow(VBoxContainer rows, string text)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        rows.AddChild(row);
        row.AddChild(new Label { Text = text, CustomMinimumSize = new Vector2(66, 0) });
        var choice = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var language in _languages) choice.AddItem(LanguageName(language));
        row.AddChild(choice);
        return choice;
    }

    private void Select(bool first, int index)
    {
        var pair = LanguageSwitcher.Preferences;
        var selected = _languages[index];
        try
        {
            if (first) LanguageSwitcher.SetPair(selected, selected == pair.Second ? pair.First : pair.Second);
            else LanguageSwitcher.SetPair(selected == pair.First ? pair.Second : pair.First, selected);
        }
        catch (Exception error)
        {
            ModEntry.Logger.Warn($"Could not save language preferences: {error}");
            ShowStatus("言語設定を保存できませんでした", 5);
            RefreshSelection();
        }
    }

    internal void RefreshSelection()
    {
        _first.Select(Array.IndexOf(_languages, LanguageSwitcher.Preferences.First));
        _second.Select(Array.IndexOf(_languages, LanguageSwitcher.Preferences.Second));
    }

    internal void ToggleSettings()
    {
        _panel.Visible = !_panel.Visible;
        if (_panel.Visible) { RefreshSelection(); _first.GrabFocus(); }
        else _panel.GetViewport().GuiReleaseFocus();
    }

    internal async void ShowStatus(string text, double seconds)
    {
        var version = ++_statusVersion;
        _status.Text = text;
        _status.Show();
        await _status.ToSignal(_status.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        if (version == _statusVersion && GodotObject.IsInstanceValid(_status)) _status.Hide();
    }
}
