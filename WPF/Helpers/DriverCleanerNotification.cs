using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ZenTimings.Helpers
{
    internal static class DriverCleanerNotification
    {
        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_DELETE = 0x00000002;
        private const uint NIM_SETVERSION = 0x00000004;

        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;
        private const uint NIF_INFO = 0x00000010;

        private const uint NIIF_INFO = 0x00000001;
        private const uint NIIF_WARNING = 0x00000002;
        private const uint NIIF_USER = 0x00000004;
        private const uint NIIF_LARGE_ICON = 0x00000020;

        private const uint NOTIFYICON_VERSION_4 = 4;

        // Stock Windows icons.
        private const int IDI_INFORMATION = 32516;
        private const int IDI_WARNING = 32515;

        private const string AppUserModelId = "irusanov.ZenTimings";
        private const string AppName = "ZenTimings";
        private const ushort VT_LPWSTR = 31;

        private static readonly Guid AppUserModelIdProperty = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
        private static readonly object identityGate = new object();
        private static bool identityApplied;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;

            public uint dwState;
            public uint dwStateMask;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;

            public uint uVersion;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;

            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropVariant
        {
            public ushort vt;
            public ushort reserved1;
            public ushort reserved2;
            public ushort reserved3;
            public IntPtr value;
            public int padding;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyKey
        {
            public Guid formatId;
            public uint propertyId;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SetCurrentProcessExplicitAppUserModelID(
            [MarshalAs(UnmanagedType.LPWStr)] string appId);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconEx(string lpszFile, int nIconIndex,
            out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

        [DllImport("ole32.dll", PreserveSig = false)]
        private static extern void PropVariantClear(ref PropVariant pvar);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int X,
            int Y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink
        {
        }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            void GetPath(IntPtr pszFile, int cch, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription(IntPtr pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory(IntPtr pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments(IntPtr pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out ushort pwHotkey);
            void SetHotkey(ushort wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation(IntPtr pszIconPath, int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, Guid("0000010b-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig]
            int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName,
                [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PropertyKey pkey);
            void GetValue(ref PropertyKey key, out PropVariant pv);
            void SetValue(ref PropertyKey key, ref PropVariant pv);
            void Commit();
        }

        public static void Show(string title, string message, bool warning = false)
        {
            IntPtr hwnd = IntPtr.Zero;
            IntPtr smallIcon = IntPtr.Zero;
            IntPtr largeIcon = IntPtr.Zero;
            bool iconAdded = false;

            ApplyIdentity();

            try
            {
                bool hasIcons = TryLoadIcons(out smallIcon, out largeIcon);
                bool useAppIcon = hasIcons && !warning;

                hwnd = CreateWindowEx(
                    0,
                    "STATIC",
                    "ZenTimings Driver Cleanup",
                    0,
                    0,
                    0,
                    0,
                    0,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero);

                if (hwnd == IntPtr.Zero)
                    return;

                NOTIFYICONDATA data = new NOTIFYICONDATA
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = hwnd,
                    uID = 1,
                    uFlags = NIF_ICON | NIF_TIP | NIF_INFO,
                    hIcon = hasIcons
                        ? smallIcon
                        : LoadIcon(IntPtr.Zero, new IntPtr(warning ? IDI_WARNING : IDI_INFORMATION)),
                    szTip = "ZenTimings Driver Cleanup",
                    szInfoTitle = title,
                    szInfo = message,
                    dwInfoFlags = warning
                        ? NIIF_WARNING
                        : useAppIcon ? NIIF_USER | NIIF_LARGE_ICON : NIIF_INFO,
                    uVersion = NOTIFYICON_VERSION_4,
                    hBalloonIcon = useAppIcon ? largeIcon : IntPtr.Zero
                };

                // Add the notification-area icon and display the balloon notification.
                if (!Shell_NotifyIcon(NIM_ADD, ref data))
                {
                    if (!hasIcons)
                        return;

                    data.hIcon = LoadIcon(IntPtr.Zero, new IntPtr(warning ? IDI_WARNING : IDI_INFORMATION));
                    data.hBalloonIcon = IntPtr.Zero;
                    data.dwInfoFlags = warning ? NIIF_WARNING : NIIF_INFO;

                    if (!Shell_NotifyIcon(NIM_ADD, ref data))
                        return;
                }

                iconAdded = true;

                // Tell Shell that we want the Windows Vista+ notification icon interface.
                Shell_NotifyIcon(NIM_SETVERSION, ref data);
            }
            catch
            {
                // Notification failure must never affect driver cleanup.
            }
            finally
            {
                if (iconAdded)
                {
                    NOTIFYICONDATA data = new NOTIFYICONDATA
                    {
                        cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                        hWnd = hwnd,
                        uID = 1
                    };

                    Shell_NotifyIcon(NIM_DELETE, ref data);
                }

                if (hwnd != IntPtr.Zero)
                    DestroyWindow(hwnd);

                if (smallIcon != IntPtr.Zero)
                    DestroyIcon(smallIcon);

                if (largeIcon != IntPtr.Zero)
                    DestroyIcon(largeIcon);
            }
        }

        private static void ApplyIdentity()
        {
            lock (identityGate)
            {
                if (identityApplied)
                    return;

                identityApplied = true;

                try
                {
                    SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
                    EnsureShortcut(ExecutablePath());
                }
                catch
                {
                }
            }
        }

        private static void EnsureShortcut(string exePath)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return;

            string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            if (string.IsNullOrEmpty(startMenu))
                return;

            string programs = Path.Combine(startMenu, "Programs");
            string shortcut = Path.Combine(programs, AppName + ".lnk");

            if (File.Exists(shortcut) && File.GetLastWriteTimeUtc(shortcut) >= File.GetLastWriteTimeUtc(exePath))
                return;

            Directory.CreateDirectory(programs);

            object link = new ShellLink();

            try
            {
                ((IShellLinkW)link).SetPath(exePath);

                PropertyKey key = new PropertyKey { formatId = AppUserModelIdProperty, propertyId = 5 };
                PropVariant id = new PropVariant
                {
                    vt = VT_LPWSTR,
                    value = Marshal.StringToCoTaskMemUni(AppUserModelId)
                };

                try
                {
                    IPropertyStore store = (IPropertyStore)link;
                    store.SetValue(ref key, ref id);
                    store.Commit();
                }
                finally
                {
                    PropVariantClear(ref id);
                }

                ((IPersistFile)link).Save(shortcut, true);
            }
            finally
            {
                Marshal.FinalReleaseComObject(link);
            }
        }

        private static bool TryLoadIcons(out IntPtr smallIcon, out IntPtr largeIcon)
        {
            smallIcon = IntPtr.Zero;
            largeIcon = IntPtr.Zero;

            string exePath = ExecutablePath();
            if (!string.IsNullOrEmpty(exePath))
                ExtractIconEx(exePath, 0, out largeIcon, out smallIcon, 1);

            return smallIcon != IntPtr.Zero && largeIcon != IntPtr.Zero;
        }

        private static string ExecutablePath()
        {
            return Assembly.GetEntryAssembly()?.Location;
        }
    }
}
