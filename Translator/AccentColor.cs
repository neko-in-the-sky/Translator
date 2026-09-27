using System;
using System.Windows.Media;
using Windows.UI.ViewManagement;

namespace Translator;

/// <summary>
/// Reads the Windows accent colour.
/// </summary>
public static class AccentColor
{
    /// <summary>
    /// Returns the darker shade of the accent colour that Windows 11's light theme uses for controls,
    /// or <see langword="null"/> if Windows can't provide it.
    /// See https://learn.microsoft.com/en-us/uwp/api/windows.ui.viewmanagement.uisettings.getcolorvalue.
    /// </summary>
    public static Color? TryGet()
    {
        try
        {
            var color = new UISettings().GetColorValue(UIColorType.AccentDark1);
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }
        catch (Exception)
        {
            // A WinRT call can fail with any COM error, e.g. on a stripped-down Windows edition.
            return null;
        }
    }
}
