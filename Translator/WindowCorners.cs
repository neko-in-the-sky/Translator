using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Translator;

/// <summary>
/// Asks the Desktop Window Manager to round a window's corners, as Windows 11 does for its own flyouts.
/// </summary>
public static class WindowCorners
{
    /// <summary>
    /// The window attribute that sets the rounded corner preference.
    /// See https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute.
    /// </summary>
    private const int DwmwaWindowCornerPreference = 33;

    /// <summary>
    /// Round the corners if appropriate.
    /// See https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_window_corner_preference.
    /// </summary>
    private const int DwmwcpRound = 2;

    /// <summary>
    /// Sets the value of a Desktop Window Manager attribute for a window.
    /// See https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmsetwindowattribute.
    /// </summary>
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        [In] IntPtr hwnd,
        [In] int dwAttribute,
        [In] ref int pvAttribute,
        [In] int cbAttribute);

    /// <summary>
    /// Rounds the corners of <paramref name="window"/>, whose handle must already exist.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> if Windows can't round them, which is the case before Windows 11.
    /// </returns>
    public static bool TryRound(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var preference = DwmwcpRound;
        return DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref preference, sizeof(int)) == 0;
    }
}
