using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Translator
{
    public class HotkeyManager
    {
        private const int HOTKEY_ID = 9000;

        private readonly Window _window;
        private readonly Action _onHotkeyPressed;

        [DllImport("User32.dll")]
        private static extern bool RegisterHotKey(
            [In] IntPtr hWnd,
            [In] int id,
            [In] uint fsModifiers,
            [In] uint vk);

        [DllImport("User32.dll")]
        private static extern bool UnregisterHotKey(
            [In] IntPtr hWnd,
            [In] int id);

        public HotkeyManager(Window window, Action onHotkeyPressed)
        {
            _window = window;
            _onHotkeyPressed = onHotkeyPressed;
        }

        public void RegisterHotKey()
        {
            var windowInteropHelper = new WindowInteropHelper(_window);
            var hwndSource = HwndSource.FromHwnd(windowInteropHelper.Handle);
            hwndSource.AddHook(HwndHook);

            uint VK_C = (uint) KeyInterop.VirtualKeyFromKey(Key.Space);
            const uint MOD_CTRL = 0x0002;
            if (!RegisterHotKey(windowInteropHelper.Handle, HOTKEY_ID, MOD_CTRL, VK_C))
            {
                // TODO
            }
        }

        public void UnregisterHotKey()
        {
            var helper = new WindowInteropHelper(_window);
            UnregisterHotKey(helper.Handle, HOTKEY_ID);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            switch (msg)
            {
                case WM_HOTKEY:
                    switch (wParam.ToInt32())
                    {
                        case HOTKEY_ID:
                            _onHotkeyPressed();
                            handled = true;
                            break;
                    }

                    break;
            }

            return IntPtr.Zero;
        }
    }
}