using BilingualSpire.Localization;
using BilingualSpire.Settings;
using BilingualSpire.UI;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;

namespace BilingualSpire;

internal static class LanguageSwitcher
{
    internal static LanguagePreferences Preferences { get; private set; } = new();
    internal static LanguageOverlay Overlay { get; private set; } = null!;
    internal static Task Ready { get; private set; } = Task.CompletedTask;
    internal static bool IsBilingual { get; private set; }
    private static bool _toggleDown;
    private static bool _settingsDown;
    private static bool _peekDown;
    private static bool _windowFocused = true;
    private sealed record DisplaySnapshot(string Language, bool Bilingual);
    private static DisplaySnapshot? _beforePeek;
    internal static bool IsPeeking => _beforePeek is not null;

    internal static void Start() => Ready = Initialize();

    private static async Task Initialize()
    {
        await NGame.Instance!.GameStartupComplete;
        Preferences = LanguagePreferences.Load();
        var hasSavedMode = Preferences.Mode is not null;
        var mode = Preferences.Mode ?? DisplayMode.First;
        Preferences = Preferences with { Mode = mode };
        Overlay = new LanguageOverlay();
        Overlay.Attach(NGame.Instance);
        var window = NGame.Instance.GetWindow();
        _windowFocused = DisplayServer.GetName() == "headless" || window.HasFocus();
        window.FocusEntered += () => _windowFocused = true;
        window.FocusExited += () =>
        {
            _windowFocused = false;
            try { EndPeek(); }
            catch (Exception error) { ReportShortcutError(error); }
        };
        NGame.Instance.GetTree().ProcessFrame += ProcessInput;
        try
        {
            SetDisplay(mode == DisplayMode.Second ? Preferences.Second : Preferences.First,
                mode == DisplayMode.Bilingual, save: false);
        }
        catch (Exception error)
        {
            ModEntry.Logger.Warn($"Could not restore display mode: {error}");
            Overlay.ShowStatus("表示モードを復元できませんでした。C / Gで選び直せます", 7);
        }
        if (!hasSavedMode) Overlay.ShowStatus("Z  長押しで翻訳   ·   C  表示切り替え   ·   G  言語設定", 7);
        ModEntry.Logger.Info($"Language switcher ready: {Preferences.First} / {Preferences.Second}; Z hold to peek, C toggle, G settings");
    }

    private static void ProcessInput()
    {
        var toggle = Input.IsPhysicalKeyPressed(Key.C);
        var settings = Input.IsPhysicalKeyPressed(Key.G);
        var peek = Input.IsPhysicalKeyPressed(Key.Z);
        var modified = Input.IsKeyPressed(Key.Meta) || Input.IsKeyPressed(Key.Ctrl)
            || Input.IsKeyPressed(Key.Alt) || Input.IsKeyPressed(Key.Shift);
        var previousToggle = _toggleDown;
        var previousSettings = _settingsDown;
        var previousPeek = _peekDown;
        _toggleDown = toggle;
        _settingsDown = settings;
        _peekDown = peek;
        try
        {
            // Track raw key edges above so releasing a modifier cannot trigger a held letter.
            // Release must restore the view even if a text field gained focus while held.
            if (!peek || !_windowFocused || modified) EndPeek();
            if (!_windowFocused || modified || NGame.Instance!.GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit) return;
            if (settings && !previousSettings) { EndPeek(); Overlay.ToggleSettings(); }
            if (toggle && !previousToggle) Toggle();
            if (peek && !previousPeek && !toggle && !settings) BeginPeek();
        }
        catch (Exception error)
        {
            ReportShortcutError(error);
        }
    }

    internal static void Toggle()
    {
        EndPeek();
        if (IsBilingual) SwitchTo(Preferences.First);
        else if (LocManager.Instance.Language == Preferences.First) SwitchTo(Preferences.Second);
        else if (LocManager.Instance.Language == Preferences.Second) ShowBilingual();
        else SwitchTo(Preferences.First);
    }

    internal static void SwitchTo(string language)
    {
        EndPeek();
        SetDisplay(language, bilingual: false);
    }

    internal static void ShowBilingual()
    {
        EndPeek();
        SetDisplay(Preferences.First, bilingual: true);
    }

    private static void BeginPeek()
    {
        if (_beforePeek is not null) return;
        _beforePeek = new(LocManager.Instance.Language, IsBilingual);
        try
        {
            if (IsBilingual || LocManager.Instance.Language != Preferences.Second)
                SetDisplay(Preferences.Second, bilingual: false, save: false, notify: false);
        }
        catch { _beforePeek = null; throw; }
    }

    internal static void EndPeek()
    {
        if (_beforePeek is not { } previous) return;
        _beforePeek = null;
        if (IsBilingual != previous.Bilingual || LocManager.Instance.Language != previous.Language)
            SetDisplay(previous.Language, previous.Bilingual, save: false, notify: false);
    }

    private static void ReportShortcutError(Exception error)
    {
        ModEntry.Logger.Error($"Language shortcut failed: {error}");
        Overlay.ShowStatus("言語を切り替えられませんでした", 5);
    }

    private static void SetDisplay(string language, bool bilingual, bool save = true, bool notify = true)
    {
        if (!LanguagePreferences.Languages.Contains(language)) throw new ArgumentException("Unsupported language", nameof(language));
        var previous = LocManager.Instance.Language;
        var previousMode = IsBilingual;
        IsBilingual = bilingual;
        try { if (previous != language) LocManager.Instance.SetLanguage(language); }
        catch
        {
            IsBilingual = previousMode;
            LocManager.Instance.SetLanguage(previous);
            throw;
        }
        var updated = LiveText.Refresh();
        Patches.CardRefreshPatch.Refresh();
        Patches.CardHoverPatch.Refresh();
        Patches.EventLayoutPatch.Refresh();
        Patches.MapLegendPatch.Refresh();
        try { Patches.AncientBannerPatch.Refresh(); }
        catch (Exception error) { LiveText.Report(error); }
        var mode = bilingual ? $"{LanguageOverlay.LanguageName(Preferences.First)} ＋ {LanguageOverlay.LanguageName(Preferences.Second)}" : LanguageOverlay.LanguageName(language);
        if (notify) Overlay.ShowStatus($"{mode}   ·   Z  長押し翻訳   C  切り替え   G  設定", 3);
        if (save)
        {
            var savedMode = bilingual ? DisplayMode.Bilingual : language == Preferences.First
                ? DisplayMode.First : language == Preferences.Second ? DisplayMode.Second : (DisplayMode?)null;
            Preferences = Preferences with { Mode = savedMode };
            try { Preferences.Save(); }
            catch (Exception error)
            {
                ModEntry.Logger.Warn($"Could not save display mode: {error}");
                Overlay.ShowStatus("表示は変更しましたが、次回起動用の設定を保存できませんでした", 6);
            }
        }
        ModEntry.Logger.Info($"Language switched: {previous} -> {language}; bilingual={bilingual}; refreshed {updated} labels");
    }

    internal static void UseGameLanguage(string language)
    {
        if (!LanguagePreferences.Languages.Contains(language)) return;
        EndPeek();
        var second = language == Preferences.Second ? Preferences.First : Preferences.Second;
        Preferences = new LanguagePreferences(language, second, IsBilingual ? DisplayMode.Bilingual : DisplayMode.First).Validated();
        Overlay.RefreshSelection();
        SetDisplay(language, IsBilingual);
    }

    internal static void SetPair(string first, string second)
    {
        EndPeek();
        var next = new LanguagePreferences(first, second, Preferences.Mode).Validated();
        next.Save();
        Preferences = next;
        Overlay.RefreshSelection();
        if (next.Mode is { } mode)
            SetDisplay(mode == DisplayMode.Second ? next.Second : next.First, mode == DisplayMode.Bilingual, save: false);
    }
}
