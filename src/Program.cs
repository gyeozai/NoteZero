using System;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Markup;

[assembly: AssemblyTitle("NoteZero")]
[assembly: AssemblyProduct("NoteZero")]
[assembly: AssemblyDescription("A minimal plain-text editor for existing files.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
// Needed because csc.exe does not add it: enables .NET 4.8 WPF behavior (per-monitor DPI, etc.).
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace NoteZero
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnLastWindowClose;
            app.Resources = LoadTheme();
            app.SessionEnding += OnSessionEnding;

            if (args.Length == 0)
            {
                Dialogs.Info("NoteZero 只用來編輯已存在的文字檔。\n\n" +
                             "請在檔案總管對檔案按右鍵 →「開啟檔案」→ NoteZero，\n" +
                             "或將 NoteZero 設為該類檔案的預設程式。");
                return 0;
            }

            int opened = 0;
            foreach (string arg in args)
            {
                if (EditorWindow.TryOpen(arg)) opened++;
            }
            if (opened == 0) return 0;
            return app.Run();
        }

        private static ResourceDictionary LoadTheme()
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("NoteZero.Theme.xaml"))
            {
                return (ResourceDictionary)XamlReader.Load(s);
            }
        }

        /// <summary>Logoff / shutdown: save every window; veto if any save fails.</summary>
        private static void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            foreach (Window w in Application.Current.Windows)
            {
                var editor = w as EditorWindow;
                if (editor != null && !editor.SaveIfDirty()) e.Cancel = true;
            }
        }
    }

    internal static class Dialogs
    {
        private const string Caption = "NoteZero";

        public static void Error(Window owner, string message)
        {
            if (owner != null && owner.IsVisible)
                MessageBox.Show(owner, message, Caption, MessageBoxButton.OK, MessageBoxImage.Error);
            else
                MessageBox.Show(message, Caption, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static void Info(string message)
        {
            MessageBox.Show(message, Caption, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
