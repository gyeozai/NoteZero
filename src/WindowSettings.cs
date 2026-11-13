using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace NoteZero
{
    /// <summary>
    /// Last window bounds (physical pixels, workspace coordinates as used by
    /// Get/SetWindowPlacement), maximized state and text zoom.
    /// Stored next to the exe (portable); falls back to %LOCALAPPDATA%\NoteZero
    /// when the exe folder is not writable.
    /// </summary>
    internal static class WindowSettings
    {
        private const string FileName = "NoteZero.settings";

        private static string ExeDirFile
        {
            get
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                return Path.Combine(dir, FileName);
            }
        }

        private static string AppDataFile
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoteZero");
                return Path.Combine(dir, FileName);
            }
        }

        /// <summary>
        /// Reads the settings line "left,top,right,bottom,maximized[,zoomPercent]".
        /// Returns true only if the saved bounds are usable on the current monitors;
        /// <paramref name="zoomPercent"/> is returned either way (100 if missing).
        /// </summary>
        public static bool TryLoad(out NativeMethods.RECT bounds, out bool maximized, out int zoomPercent)
        {
            bounds = new NativeMethods.RECT();
            maximized = false;
            zoomPercent = 100;
            string text = null;
            try
            {
                if (File.Exists(ExeDirFile)) text = File.ReadAllText(ExeDirFile);
                else if (File.Exists(AppDataFile)) text = File.ReadAllText(AppDataFile);
            }
            catch { }
            if (text == null) return false;

            string[] parts = text.Trim().Split(',');
            if (parts.Length < 5) return false;
            int[] v = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v[i]))
                    return false;
            }
            if (v.Length >= 6) zoomPercent = v[5];
            bounds = new NativeMethods.RECT(v[0], v[1], v[2], v[3]);
            maximized = v[4] != 0;
            return bounds.Right - bounds.Left >= 100 && bounds.Bottom - bounds.Top >= 60 && IsReachable(bounds);
        }

        public static void Save(IntPtr hwnd, int zoomPercent)
        {
            var wp = new NativeMethods.WINDOWPLACEMENT();
            wp.length = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.WINDOWPLACEMENT));
            if (!NativeMethods.GetWindowPlacement(hwnd, ref wp)) return;

            bool maximized = wp.showCmd == NativeMethods.SW_SHOWMAXIMIZED ||
                             (wp.showCmd == NativeMethods.SW_SHOWMINIMIZED &&
                              (wp.flags & NativeMethods.WPF_RESTORETOMAXIMIZED) != 0);
            NativeMethods.RECT r = wp.rcNormalPosition;
            string line = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5}",
                                        r.Left, r.Top, r.Right, r.Bottom, maximized ? 1 : 0, zoomPercent);
            try
            {
                File.WriteAllText(ExeDirFile, line);
                return;
            }
            catch { }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AppDataFile));
                File.WriteAllText(AppDataFile, line);
            }
            catch { }
        }

        public static void Apply(IntPtr hwnd, NativeMethods.RECT bounds)
        {
            var wp = new NativeMethods.WINDOWPLACEMENT();
            wp.length = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.WINDOWPLACEMENT));
            NativeMethods.GetWindowPlacement(hwnd, ref wp);
            wp.flags = 0;
            wp.showCmd = NativeMethods.SW_HIDE; // window is not shown yet; Show() comes later
            wp.rcNormalPosition = bounds;
            NativeMethods.SetWindowPlacement(hwnd, ref wp);
        }

        /// <summary>True if a usable part of the title bar lies inside some monitor's work area.</summary>
        private static bool IsReachable(NativeMethods.RECT r)
        {
            // Workspace -> screen coordinates (they differ when the taskbar is at the top/left).
            var origin = new NativeMethods.POINT();
            NativeMethods.MONITORINFO primary;
            if (!NativeMethods.TryGetMonitorInfo(
                    NativeMethods.MonitorFromPoint(origin, NativeMethods.MONITOR_DEFAULTTOPRIMARY), out primary))
                return false;
            int dx = primary.rcWork.Left - primary.rcMonitor.Left;
            int dy = primary.rcWork.Top - primary.rcMonitor.Top;

            var strip = new NativeMethods.RECT(r.Left + dx, r.Top + dy, r.Right + dx, r.Top + dy + 32);
            NativeMethods.MONITORINFO mi;
            if (!NativeMethods.TryGetMonitorInfo(
                    NativeMethods.MonitorFromRect(ref strip, NativeMethods.MONITOR_DEFAULTTONULL), out mi))
                return false;

            int w = Math.Min(strip.Right, mi.rcWork.Right) - Math.Max(strip.Left, mi.rcWork.Left);
            int h = Math.Min(strip.Bottom, mi.rcWork.Bottom) - Math.Max(strip.Top, mi.rcWork.Top);
            return w >= 120 && h >= 20;
        }
    }
}
