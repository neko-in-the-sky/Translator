namespace Translator.Configuration;

/// <summary>
/// The settings a user may override from their own settings file, which updates never touch.
/// Everything else in <see cref="ApplicationSettings"/> is controlled by the release.
/// </summary>
public class UserSettings
{
    public string DefaultSearchEngine { get; set; }

    public string Culture { get; set; }

    public string[] AllowedFullscreenApps { get; set; }

    public PopupSettings Popup { get; set; }
}
