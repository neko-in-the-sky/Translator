using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Translator.Configuration;

namespace Translator;

/// <summary>
/// Allows to check whether showing a pop-up window is allowed.
/// In particular, this checker allow to disable pop-ups when a fullscreen app is running (e.g. a video game).
/// </summary>
public class NotificationStateChecker
{
    private readonly ILogger<NotificationStateChecker> _logger;
    private readonly HashSet<string> _allowedFullScreenApps;

    public NotificationStateChecker(IOptions<ApplicationSettings> applicationSettings,
        ILogger<NotificationStateChecker> logger)
    {
        _allowedFullScreenApps = new(applicationSettings.Value.AllowedFullscreenApps);
        _logger = logger;
        
        _logger.LogInformation("Allowed fullscreen apps: {@apps}", _allowedFullScreenApps);
    }

    /// <summary>
    /// Checks the state of the computer for the current user to determine whether sending a notification is appropriate.
    /// See https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shqueryusernotificationstate
    /// </summary>
    [DllImport("shell32.dll")]
    private static extern uint SHQueryUserNotificationState([Out] out QueryUserNotificationState state);

    /// <summary>
    /// Retrieves a handle to the foreground window (the window with which the user is currently working).
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow
    /// </summary>
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// Retrieves the identifier of the thread that created the specified window and, optionally, the identifier of the
    /// process that created the window.
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowthreadprocessid
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>
    /// Checks whether showing a pop-up window is allowed.
    /// </summary>
    public bool AreNotificationsAllowed()
    {
        try
        {
            var res = SHQueryUserNotificationState(out var state);
            if (res != 0)
            {
                _logger.LogError("SHQueryUserNotificationState. Invalid result: {Result}", res);
                return false;
            }

            _logger.LogInformation("SHQueryUserNotificationState returned {State}", state);

            switch (state)
            {
                case QueryUserNotificationState.QunsAcceptsNotifications:
                case QueryUserNotificationState.QunsQuietTime:
                    return true;
                case QueryUserNotificationState.QunsBusy:
                {
                    var hWnd = GetForegroundWindow();
                    if (hWnd != 0)
                    {
                        res = GetWindowThreadProcessId(hWnd, out var pid);
                        if (res != 0)
                        {
                            var process = Process.GetProcessById((int)pid);
                            if (_allowedFullScreenApps.Any(a => process.ProcessName.Contains(a)))
                            {
                                _logger.LogInformation(
                                    "An allowed fullscreen window is running: {ProcessName}", process.ProcessName);
                                return true;
                            }
                        }
                    }

                    break;
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to check whether notifications are allowed");
        }

        return false;
    }

    /// <summary>
    /// Specifies the state of the machine for the current user in relation to the propriety of sending a notification.
    /// Used by SHQueryUserNotificationState.
    /// See https://learn.microsoft.com/en-us/windows/win32/api/shellapi/ne-shellapi-query_user_notification_state
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private enum QueryUserNotificationState
    {
        /// <summary>
        /// A screen saver is displayed, the machine is locked, or a nonactive Fast User Switching session is in
        /// progress.
        /// </summary>
        QunsNotPresent = 1,

        /// <summary>
        /// A full-screen application is running or Presentation Settings are applied.
        /// Presentation Settings allow a user to put their machine into a state fit for an uninterrupted presentation,
        /// such as a set of PowerPoint slides, with a single click.
        /// </summary>
        QunsBusy = 2,

        /// <summary>
        /// A full-screen (exclusive mode) Direct3D application is running.
        /// </summary>
        QunsRunningD3DFullScreen = 3,

        /// <summary>
        /// The user has activated Windows presentation settings to block notifications and pop-up messages.
        /// </summary>
        QunsPresentationMode = 4,

        /// <summary>
        /// None of the other states are found, notifications can be freely sent.
        /// </summary>
        QunsAcceptsNotifications = 5,

        /// <summary>
        /// Introduced in Windows 7. The current user is in "quiet time", which is the first hour after a new user logs
        /// into his or her account for the first time. During this time, most notifications should not be sent or
        /// shown. This lets a user become accustomed to a new computer system without those distractions. Quiet time
        /// also occurs for each user after an operating system upgrade or clean installation.
        /// </summary>
        QunsQuietTime = 6,

        /// <summary>
        /// Introduced in Windows 8. A Windows Store app is running.
        /// </summary>
        QunsApp = 7
    }
}