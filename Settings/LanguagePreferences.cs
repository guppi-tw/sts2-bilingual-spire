using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MegaCrit.Sts2.Core.Localization;

namespace BilingualSpire.Settings;

[JsonConverter(typeof(JsonStringEnumConverter<DisplayMode>))]
internal enum DisplayMode { First, Second, Bilingual }

internal sealed record LanguagePreferences(string First = "eng", string Second = "jpn", DisplayMode? Mode = null)
{
    internal static string[] Languages => LocManager.Languages.Where(l => !LocManager.IsHiddenLanguage(l)).ToArray();
    internal static string ConfigPath => System.Environment.GetEnvironmentVariable("BILINGUAL_SPIRE_CONFIG_PATH")
        ?? ProjectSettings.GlobalizePath("user://mod_configs/BilingualSpire.json");

    internal LanguagePreferences Validated()
    {
        var first = Languages.Contains(First) ? First : "eng";
        var second = Languages.Contains(Second) && Second != first ? Second : first == "jpn" ? "eng" : "jpn";
        var mode = Mode is { } value && Enum.IsDefined(value) ? Mode : null;
        return new(first, second, mode);
    }

    internal static LanguagePreferences Load()
    {
        try
        {
            return (File.Exists(ConfigPath)
                ? JsonSerializer.Deserialize<LanguagePreferences>(File.ReadAllText(ConfigPath)) ?? new()
                : new()).Validated();
        }
        catch (Exception error)
        {
            ModEntry.Logger.Warn($"Could not load language preferences; using English/Japanese: {error.Message}");
            return new();
        }
    }

    internal void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var temporary = ConfigPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, ConfigPath, overwrite: true);
    }
}
