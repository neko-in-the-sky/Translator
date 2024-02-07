using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Translator.Configuration;

namespace Translator;

/// <summary>
/// Determines the location and the size of the pop-up.
/// </summary>
public class PopupSizeLocationProvider(
    ILogger<PopupSizeLocationProvider> logger,
    IOptions<ApplicationSettings> applicationSettings)
{
    private readonly PopupSettings _popupSettings = applicationSettings.Value.Popup;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out PointUser32 lpPoint);

    public (double, double) GetWidthAndHeight()
    {
        var (screenWidth, screenHeight) = GetScreenWidthAndHeight();
        var maxPossibleHeight = (screenHeight - _popupSettings.VerticalOffsetFromCursor) / 2;
        return (Math.Min(_popupSettings.DefaultWidth, screenWidth / 2),
            Math.Min(_popupSettings.DefaultHeight, maxPossibleHeight));
    }

    public (double, double) GetLeftAndTop(Window window)
    {
        var (screenWidth, screenHeight) = GetScreenWidthAndHeight();
        GetCursorPos(out var cursorPositionPixels);
        var mousePosition = ScreenPixelsToDip(cursorPositionPixels, window);

        var top = mousePosition.Y + _popupSettings.VerticalOffsetFromCursor + window.Height > screenHeight
            ? mousePosition.Y - _popupSettings.VerticalOffsetFromCursor - window.Height
            : mousePosition.Y + _popupSettings.VerticalOffsetFromCursor;
        var left = mousePosition.X + window.Width > screenWidth
            ? screenWidth - window.Width
            : mousePosition.X;

        return (left, top);
    }

    private (double, double) GetScreenWidthAndHeight()
    {
        var screenSize = (SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
        logger.LogInformation("Screen size is {ScreenSize}", screenSize);
        return screenSize;
    }

    private static Point ScreenPixelsToDip(Point p, Visual visual)
    {
        var t = PresentationSource.FromVisual(visual).CompositionTarget.TransformFromDevice;
        return t.Transform(p);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointUser32
    {
        public int X;
        public int Y;

        public static implicit operator Point(PointUser32 point)
        {
            return new Point(point.X, point.Y);
        }
    }
}