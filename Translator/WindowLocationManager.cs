using System.Runtime.InteropServices;
using System.Windows;

namespace Translator
{
    public class WindowLocationManager
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out PointUser32 lpPoint);

        private readonly Window _window;

        public WindowLocationManager(Window window)
        {
            _window = window;
        }

        public (double, double) GetLeftAndTop()
        {
            var screenWidthPix = SystemParameters.PrimaryScreenWidth * 0.9;
            var screenHeightPix = SystemParameters.PrimaryScreenHeight * 0.9;
            var screenWidthAndHeightDip = ScreenPixelsToDip(new Point(screenWidthPix, screenHeightPix));

            GetCursorPos(out var mousePositionPix);
            var mousePositionDip = ScreenPixelsToDip(mousePositionPix);

            const double offsetDip = 25;

            var top = mousePositionDip.Y + offsetDip + _window.Height > screenWidthAndHeightDip.Y
                ? mousePositionDip.Y - offsetDip - _window.Height
                : mousePositionDip.Y + offsetDip;
            var left = mousePositionDip.X + _window.Width > screenWidthAndHeightDip.X
                ? mousePositionDip.X - _window.Width
                : mousePositionDip.X;

            return (left, top);
        }

        private Point ScreenPixelsToDip(Point p)
        {
            var t = PresentationSource.FromVisual(_window).CompositionTarget.TransformFromDevice;
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
}