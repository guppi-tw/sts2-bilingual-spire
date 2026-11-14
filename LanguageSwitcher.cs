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

    internal static void Start() => Ready = Initialize();

    private static async Task Initialize()
    {
        await NGame.Instance!.GameStartupComplete;
        Preferences = LanguagePreferences.Load();
        Overlay = new LanguageOverlay();
        Overlay.Attach(NGame.Instance);
        NGame.Instance.GetTree().ProcessFrame += ProcessInput;
        if (Preferences.Mode is { } mode)
        {
            try { SetDisplay(mode == DisplayMode.Second ? Preferences.Second : Preferences.First,
                mode == DisplayMode.Bilingual, save: false); }
            catch (Exception error)
            {
                ModEntry.Logger.Warn($"Could not restore display mode: {error}");
                Overlay.ShowStatus("表示モードを復元できませんでした。F8 / F9で選び直せます", 7);
            }
        }
        if (Preferences.Mode is null) Overlay.ShowStatus("F8  表示モード切り替え   ·   F9  言語設定", 7);
        ModEntry.Logger.Info($"Language switcher ready: {Preferences.First} / {Preferences.Second}; F8 toggle, F9 settings");
    }

    private static void ProcessInput()
    {
        var toggle = Input.IsPhysicalKeyPressed(Key.F8);
        var settings = Input.IsPhysicalKeyPressed(Key.F9);
        var previousToggle = _toggleDown;
        var previousSettings = _settingsDown;
        _toggleDown = toggle;
        _settingsDown = settings;
        if (NGame.Instance!.GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit) return;
        try
        {
            if (settings && !previousSettings) Overlay.ToggleSettings();
            if (toggle && !previousToggle) Toggle();
        }
        catch (Exception error)
        {
            ModEntry.Logger.Error($"Language shortcut failed: {error}");
            Overlay.ShowStatus("言語を切り替えられませんでした", 5);
        }
    }

    internal static void Toggle()
    {
        if (IsBilingual) SwitchTo(Preferences.First);
        else if (LocManager.Instance.Language == Preferences.First) SwitchTo(Preferences.Second);
        else if (LocManager.Instance.Language == Preferences.Second) ShowBilingual();
        else SwitchTo(Preferences.First);
    }

    internal static void SwitchTo(string language) => SetDisplay(language, bilingual: false);
    internal static void ShowBilingual() => SetDisplay(Preferences.First, bilingual: true);

    private static void SetDisplay(string language, bool bilingual, bool save = true)
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
        Patches.EventLayoutPatch.Refresh();
        try { Patches.AncientBannerPatch.Refresh(); }
        catch (Exception error) { LiveText.Report(error); }
        var mode = bilingual ? $"{LanguageOverlay.LanguageName(Preferences.First)} ＋ {LanguageOverlay.LanguageName(Preferences.Second)}" : LanguageOverlay.LanguageName(language);
        Overlay.ShowStatus($"{mode}   ·   F8  表示切り替え   F9  言語設定", 3);
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
        var second = language == Preferences.Second ? Preferences.First : Preferences.Second;
        Preferences = new LanguagePreferences(language, second, IsBilingual ? DisplayMode.Bilingual : DisplayMode.First).Validated();
        Overlay.RefreshSelection();
        SetDisplay(language, IsBilingual);
    }

    internal static void SetPair(string first, string second)
    {
        var next = new LanguagePreferences(first, second, Preferences.Mode).Validated();
        next.Save();
        Preferences = next;
        Overlay.RefreshSelection();
        if (next.Mode is { } mode)
            SetDisplay(mode == DisplayMode.Second ? next.Second : next.First, mode == DisplayMode.Bilingual, save: false);
    }
}
