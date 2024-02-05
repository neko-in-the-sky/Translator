using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Translator;

public class HotkeyManager(Window window, Action onHotkeyPressed)
{
    private const int HotkeyId = 0;

    private const uint HotkeyModCtrl = 0x0002;
    private const Key Hotkey = Key.Space;

    /// <summary>
    /// Posted when the user presses a hot key registered by the RegisterHotKey function. The message is placed at
    /// the top of the message queue associated with the thread that registered the hot key.
    /// See https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey".
    /// </summary>
    private const int WmHotkeyMessageId = 0x0312;

    /// <summary>
    /// Defines a system-wide hot key.
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey.
    /// </summary>
    [DllImport("User32.dll")]
    private static extern bool RegisterHotKey(
        [In] IntPtr hWnd,
        [In] int id,
        [In] uint fsModifiers,
        [In] uint vk);

    /// <summary>
    /// Frees a hot key previously registered by the calling thread.
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-unregisterhotkey.
    /// </summary>
    [DllImport("User32.dll")]
    private static extern bool UnregisterHotKey(
        [In] IntPtr hWnd,
        [In] int id);

    public void RegisterHotKey()
    {
        var windowInteropHelper = new WindowInteropHelper(window);
        var hwndSource = HwndSource.FromHwnd(windowInteropHelper.Handle);
        hwndSource!.AddHook(HwndHook);

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(Hotkey);
        if (!RegisterHotKey(windowInteropHelper.Handle, HotkeyId, HotkeyModCtrl, virtualKey))
        {
            new ToastContentBuilder()
                .AddText(Properties.Resources.Notification_FailedToAddHotkey)
                .Show();
        }
        else
        {
            new ToastContentBuilder()
                .AddText(Properties.Resources.Notification_AddedHotkey)
                .Show();
        }
    }

    public void UnregisterHotKey()
    {
        var windowInteropHelper = new WindowInteropHelper(window);
        UnregisterHotKey(windowInteropHelper.Handle, HotkeyId);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkeyMessageId && wParam.ToInt32() == HotkeyId)
        {
            onHotkeyPressed();
            handled = true;
        }

        return IntPtr.Zero;
    }
}