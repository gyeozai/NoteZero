using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace NoteZero
{
    internal sealed class EditorWindow : Window
    {
        private const double TitleBarHeight = 32;
        private const double ResizeBorder = 5;
        private const string FileProp = "NoteZero.FileHash";
        private const int MinZoom = 50, MaxZoom = 300, ZoomStep = 10; // percent

        private readonly TextFile file;
        private readonly int fileHash;
        private readonly Grid root;
        private readonly TextBlock titleText;
        private readonly TextBox editor;
        private readonly WindowChrome chrome;

        private IntPtr hwnd;
        private string savedLf;
        private bool dirty;
        private bool dirtyCheckPending;

        private readonly double baseFontSize;   // from Theme.xaml, i.e. 100 %
        private readonly double baseLineHeight;
        private int zoomPercent = 100;
        private int wheelDelta;                 // accumulates high-resolution wheel/touchpad input

        // ------------------------------------------------------------------ open

        /// <summary>Opens <paramref name="arg"/> in a new window. Returns true if a window was created.</summary>
        public static bool TryOpen(string arg)
        {
            string path;
            try
            {
                path = Path.GetFullPath(arg.Trim('"'));
            }
            catch (Exception ex)
            {
                Dialogs.Error(null, "無法開啟「" + arg + "」。\n\n" + ex.Message);
                return false;
            }

            if (Directory.Exists(path))
            {
                Dialogs.Error(null, "這是資料夾，不是檔案：\n" + path);
                return false;
            }
            if (!File.Exists(path))
            {
                Dialogs.Error(null, "找不到檔案：\n" + path + "\n\nNoteZero 只能開啟已存在的檔案。");
                return false;
            }

            int hash = HashPath(path);
            if (ActivateExisting(hash)) return false; // already open: one file, one window

            string text;
            TextFile file;
            try
            {
                file = TextFile.Load(path, out text);
            }
            catch (InvalidDataException)
            {
                Dialogs.Error(null, "「" + Path.GetFileName(path) + "」不是有效的 UTF-8 文字檔。\n\n" +
                                    "為避免存檔時損壞內容，NoteZero 不會開啟它。\n\n" + path);
                return false;
            }
            catch (Exception ex)
            {
                Dialogs.Error(null, "無法讀取「" + Path.GetFileName(path) + "」。\n\n" + TextFile.Describe(ex));
                return false;
            }

            var window = new EditorWindow(file, text, hash);
            window.ShowWithSavedPlacement();
            return true;
        }

        // ------------------------------------------------------------------ construction

        private EditorWindow(TextFile file, string text, int fileHash)
        {
            this.file = file;
            this.fileHash = fileHash;
            savedLf = TextFile.ToLf(text);

            Background = Brushes.White;
            ResizeMode = ResizeMode.CanResize;   // keeps WS_MAXIMIZEBOX: Snap, Win+Arrow, double-click
            WindowStyle = WindowStyle.SingleBorderWindow;
            MinWidth = 240;
            MinHeight = 120;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            chrome = new WindowChrome();
            chrome.CaptionHeight = TitleBarHeight - ResizeBorder; // WindowChrome adds the top resize border
            chrome.ResizeBorderThickness = new Thickness(ResizeBorder);
            chrome.GlassFrameThickness = new Thickness(0, 0, 0, 1); // non-zero: DWM shadow
            chrome.CornerRadius = new CornerRadius(0);
            chrome.UseAeroCaptionButtons = false;
            WindowChrome.SetWindowChrome(this, chrome);

            var textFont = (FontFamily)Application.Current.FindResource("TextFont");

            // --- title bar
            titleText = new TextBlock();
            titleText.Margin = new Thickness(18, 0, 8, 0); // lines up with the text column
            titleText.VerticalAlignment = VerticalAlignment.Center;
            titleText.FontFamily = textFont;
            titleText.FontSize = 14;
            titleText.Foreground = new SolidColorBrush(Color.FromRgb(0x5F, 0x5F, 0x5F));
            titleText.TextTrimming = TextTrimming.CharacterEllipsis;

            var minButton = new Button();
            minButton.Content = ""; // ChromeMinimize
            minButton.Style = (Style)Application.Current.FindResource("CaptionButton");
            AutomationProperties.SetName(minButton, "最小化");
            minButton.Click += delegate { WindowState = WindowState.Minimized; };

            var closeButton = new Button();
            closeButton.Content = ""; // ChromeClose
            closeButton.Style = (Style)Application.Current.FindResource("CloseButton");
            AutomationProperties.SetName(closeButton, "關閉");
            closeButton.Click += delegate { Close(); };

            var titleBar = new Grid();
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(minButton, 1);
            Grid.SetColumn(closeButton, 2);
            titleBar.Children.Add(titleText);
            titleBar.Children.Add(minButton);
            titleBar.Children.Add(closeButton);

            // --- editor
            editor = new TextBox();
            editor.Style = (Style)Application.Current.FindResource("EditorTextBox");
            editor.IsUndoEnabled = false;
            editor.Text = text;
            editor.IsUndoEnabled = true; // toggling clears the undo stack: loading is not undoable
            editor.TextChanged += OnTextChanged;
            editor.PreviewMouseWheel += OnEditorMouseWheel;
            baseFontSize = editor.FontSize;
            baseLineHeight = TextBlock.GetLineHeight(editor);

            // --- layout
            root = new Grid();
            root.Background = Brushes.White;
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TitleBarHeight) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(editor, 1);
            root.Children.Add(titleBar);
            root.Children.Add(editor);
            Content = root;

            CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, delegate { SaveIfDirty(); }));
            PreviewKeyDown += OnZoomKeyDown;

            SizeChanged += delegate { UpdateMaximizedLayout(); };
            StateChanged += delegate { UpdateMaximizedLayout(); };
            LocationChanged += delegate { UpdateMaximizedLayout(); };
            Loaded += delegate
            {
                editor.CaretIndex = 0;
                Keyboard.Focus(editor);
            };

            UpdateTitle();
        }

        private void ShowWithSavedPlacement()
        {
            NativeMethods.RECT bounds;
            bool maximized;
            int zoom;
            bool restore = WindowSettings.TryLoad(out bounds, out maximized, out zoom);
            SetZoom(zoom);
            if (!restore)
            {
                Width = 760;
                Height = 560;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            hwnd = new WindowInteropHelper(this).EnsureHandle(); // create the HWND without showing it
            NativeMethods.UseRoundCorners(hwnd);
            NativeMethods.SetProp(hwnd, FileProp, new IntPtr(fileHash));

            if (restore)
            {
                WindowSettings.Apply(hwnd, bounds);
                if (maximized) WindowState = WindowState.Maximized;
            }
            Show();
        }

        // ------------------------------------------------------------------ maximized fix

        /// <summary>
        /// A maximized WindowChrome window is larger than the work area by the (invisible)
        /// frame width on every side. Pad the content back inside the visible area.
        /// </summary>
        private void UpdateMaximizedLayout()
        {
            Thickness margin = new Thickness(0);
            if (WindowState == WindowState.Maximized && hwnd != IntPtr.Zero)
            {
                NativeMethods.RECT wr;
                NativeMethods.MONITORINFO mi;
                if (NativeMethods.GetWindowRect(hwnd, out wr) &&
                    NativeMethods.TryGetMonitorInfo(
                        NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST), out mi))
                {
                    DpiScale dpi = VisualTreeHelper.GetDpi(this);
                    margin = new Thickness(
                        Math.Max(0, mi.rcWork.Left - wr.Left) / dpi.DpiScaleX,
                        Math.Max(0, mi.rcWork.Top - wr.Top) / dpi.DpiScaleY,
                        Math.Max(0, wr.Right - mi.rcWork.Right) / dpi.DpiScaleX,
                        Math.Max(0, wr.Bottom - mi.rcWork.Bottom) / dpi.DpiScaleY);
                }
            }
            if (root.Margin != margin) root.Margin = margin;

            // Caption hit-test area is measured from the real (off-screen) window top.
            double caption = TitleBarHeight - ResizeBorder + margin.Top;
            if (chrome.CaptionHeight != caption) chrome.CaptionHeight = caption;
        }

        // ------------------------------------------------------------------ zoom

        /// <summary>Ctrl+= / Ctrl++ zoom in, Ctrl+- zoom out, Ctrl+0 reset (main keys or numpad).</summary>
        private void OnZoomKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != ModifierKeys.Control) return;
            Key key = e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;
            switch (key)
            {
                case Key.OemPlus:
                case Key.Add:
                    SetZoom(zoomPercent + ZoomStep);
                    break;
                case Key.OemMinus:
                case Key.Subtract:
                    SetZoom(zoomPercent - ZoomStep);
                    break;
                case Key.D0:
                case Key.NumPad0:
                    SetZoom(100);
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }

        /// <summary>Ctrl + mouse wheel zooms instead of scrolling.</summary>
        private void OnEditorMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                wheelDelta = 0;
                return;
            }
            e.Handled = true;
            wheelDelta += e.Delta;
            while (wheelDelta >= Mouse.MouseWheelDeltaForOneLine)
            {
                wheelDelta -= Mouse.MouseWheelDeltaForOneLine;
                SetZoom(zoomPercent + ZoomStep);
            }
            while (wheelDelta <= -Mouse.MouseWheelDeltaForOneLine)
            {
                wheelDelta += Mouse.MouseWheelDeltaForOneLine;
                SetZoom(zoomPercent - ZoomStep);
            }
        }

        /// <summary>Scales font size and line height together, keeping the line spacing ratio.</summary>
        private void SetZoom(int percent)
        {
            zoomPercent = Math.Max(MinZoom, Math.Min(MaxZoom, percent));
            double factor = zoomPercent / 100.0;
            editor.FontSize = baseFontSize * factor;
            TextBlock.SetLineHeight(editor, Math.Round(baseLineHeight * factor));
        }

        // ------------------------------------------------------------------ dirty state

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            // Coalesce bursts of typing: compare once the input queue is idle.
            if (dirtyCheckPending) return;
            dirtyCheckPending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
            {
                dirtyCheckPending = false;
                SetDirty(TextFile.ToLf(editor.Text) != savedLf);
            }));
        }

        private void SetDirty(bool value)
        {
            if (dirty == value) return;
            dirty = value;
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            string title = file.FileName + (dirty ? "*" : "");
            titleText.Text = title;
            Title = title;
        }

        // ------------------------------------------------------------------ save / close

        /// <summary>Writes the file if its content differs from disk. Returns false on failure.</summary>
        public bool SaveIfDirty()
        {
            string lf = TextFile.ToLf(editor.Text);
            if (lf == savedLf)
            {
                SetDirty(false);
                return true;
            }
            try
            {
                file.Save(lf);
            }
            catch (Exception ex)
            {
                SetDirty(true);
                Dialogs.Error(this, "無法儲存「" + file.FileName + "」，視窗不會關閉。\n\n" +
                                    TextFile.Describe(ex) + "\n\n" + file.FullPath);
                return false;
            }
            savedLf = lf;
            SetDirty(false);
            return true;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (e.Cancel) return;
            if (!SaveIfDirty())
            {
                e.Cancel = true;
                return;
            }
            WindowSettings.Save(hwnd, zoomPercent);
            NativeMethods.RemoveProp(hwnd, FileProp);
        }

        // ------------------------------------------------------------------ single window per file

        private static int HashPath(string fullPath)
        {
            // FNV-1a over the case-insensitive path; stable across processes.
            string s = fullPath.ToUpperInvariant();
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s)
                {
                    h ^= c;
                    h *= 16777619;
                }
                int result = (int)h;
                return result == 0 ? 1 : result;
            }
        }

        private static bool ActivateExisting(int hash)
        {
            IntPtr found = IntPtr.Zero;
            NativeMethods.EnumWindows(delegate(IntPtr h, IntPtr lParam)
            {
                if (NativeMethods.GetProp(h, FileProp) == new IntPtr(hash))
                {
                    found = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            if (found == IntPtr.Zero) return false;
            if (NativeMethods.IsIconic(found)) NativeMethods.ShowWindow(found, NativeMethods.SW_RESTORE);
            NativeMethods.SetForegroundWindow(found);
            return true;
        }
    }
}
