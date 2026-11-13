using System;
using System.Runtime.InteropServices;

namespace NoteZero
{
    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                Left = left; Top = top; Right = right; Bottom = bottom;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X, Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        public const int SW_HIDE = 0;
        public const int SW_SHOWMINIMIZED = 2;
        public const int SW_SHOWMAXIMIZED = 3;
        public const int SW_RESTORE = 9;
        public const int WPF_RESTORETOMAXIMIZED = 2;

        public const int MONITOR_DEFAULTTONULL = 0;
        public const int MONITOR_DEFAULTTOPRIMARY = 1;
        public const int MONITOR_DEFAULTTONEAREST = 2;

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowPlacement(IntPtr hwnd, ref WINDOWPLACEMENT placement);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPlacement(IntPtr hwnd, [In] ref WINDOWPLACEMENT placement);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromRect([In] ref RECT rect, int flags);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProp(IntPtr hwnd, string name, IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetProp(IntPtr hwnd, string name);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr RemoveProp(IntPtr hwnd, string name);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hwnd, int cmd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hwnd);

        /// <summary>Windows 11 native rounded corners. No-op on Windows 10.</summary>
        public static void UseRoundCorners(IntPtr hwnd)
        {
            int pref = DWMWCP_ROUND;
            try { DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int)); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        public static bool TryGetMonitorInfo(IntPtr monitor, out MONITORINFO info)
        {
            info = new MONITORINFO();
            info.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            return monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info);
        }
    }
}
