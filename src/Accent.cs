// Hallow Mixer - v3.17 Hallow family: the shared accent colour (%APPDATA%\Hallow\theme.json, written by Hallow Toolkit)
// and crash reports kept on this PC (%APPDATA%\HallowMixer\crashes). Channel colours never change - only the app's accent.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace HallowMixer
{
    public static class Accent
    {
        public static readonly Color Own = Color.FromRgb(0xF0, 0x78, 0x20);
        // Unfrozen brushes shared by every window: changing their colour recolours everything that uses them, live.
        public static readonly SolidColorBrush Brush = new SolidColorBrush(Own), Hi = new SolidColorBrush(Lighter(Own)), Text = new SolidColorBrush(TextOn(Own));
        public static Color Current { get { return Brush.Color; } }
        public static string OverrideFile;   // --theme-file <path>: developer testing only
        public static string File { get { return OverrideFile ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Hallow\theme.json"); } }

        static Color Lighter(Color c) { return Color.FromRgb((byte)(c.R + (255 - c.R) * 0.25), (byte)(c.G + (255 - c.G) * 0.25), (byte)(c.B + (255 - c.B) * 0.25)); }
        static Color TextOn(Color c) { double l = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255; return l > 0.45 ? Color.FromRgb(0x1A, 0x0E, 0x04) : Color.FromRgb(0xFF, 0xFF, 0xFF); }

        // The styles in Theme.xaml use these through DynamicResource. WPF freezes brushes kept in resources, so they get new
        // brushes on every change; the code-built windows use the (never frozen) Brush / Hi / Text above.
        static Application app;
        public static void Install(Application a) { app = a; Publish(); }
        static void Publish()
        {
            if (app == null) return;
            app.Resources["AccentBrush"] = new SolidColorBrush(Brush.Color); app.Resources["AccentHiBrush"] = new SolidColorBrush(Hi.Color); app.Resources["AccentTextBrush"] = new SolidColorBrush(Text.Color);
        }
        // The accent from theme.json, or null (missing, unreadable or not #RRGGBB). Never throws.
        public static Color? Read()
        {
            try
            {
                var f = File; if (!System.IO.File.Exists(f)) return null;
                string text;
                using (var s = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (s.Length > 64 * 1024) return null;
                    text = new StreamReader(s, Encoding.UTF8).ReadToEnd();
                }
                var m = Regex.Match(text, "\"accent\"\\s*:\\s*\"#([0-9a-fA-F]{6})\"");
                if (!m.Success) return null;
                int v = Convert.ToInt32(m.Groups[1].Value, 16);
                return Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
            }
            catch { return null; }
        }
        public static void Apply(bool useHallow)
        {
            Color c = Own;
            if (useHallow) { var f = Read(); if (f.HasValue) c = f.Value; }
            if (Brush.Color == c) return;
            Brush.Color = c; Hi.Color = Lighter(c); Text.Color = TextOn(c);
            Publish();
        }

        // Watches the folder (300 ms debounce, applied on the UI thread); a slow check every 5 s also catches the folder appearing later.
        static FileSystemWatcher fsw; static DispatcherTimer debounce, slow; static DateTime lastStamp;
        public static void Watch(Dispatcher ui, Func<bool> useHallow)
        {
            debounce = new DispatcherTimer(DispatcherPriority.Normal, ui) { Interval = TimeSpan.FromMilliseconds(300) };
            debounce.Tick += (s, e) => { debounce.Stop(); try { Apply(useHallow()); } catch { } };
            slow = new DispatcherTimer(DispatcherPriority.Background, ui) { Interval = TimeSpan.FromSeconds(5) };
            slow.Tick += (s, e) =>
            {
                try
                {
                    var t = System.IO.File.Exists(File) ? System.IO.File.GetLastWriteTimeUtc(File) : DateTime.MinValue;
                    if (t != lastStamp) { lastStamp = t; Apply(useHallow()); }
                    if (fsw == null) StartWatcher(ui);
                }
                catch { }
            };
            try { lastStamp = System.IO.File.Exists(File) ? System.IO.File.GetLastWriteTimeUtc(File) : DateTime.MinValue; } catch { }
            StartWatcher(ui);
            slow.Start();
        }
        static void StartWatcher(Dispatcher ui)
        {
            try
            {
                var dir = Path.GetDirectoryName(File); if (!Directory.Exists(dir)) return;
                var w = new FileSystemWatcher(dir) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size, IncludeSubdirectories = false };
                FileSystemEventHandler h = (s, e) =>
                {
                    if (!string.Equals(e.Name, Path.GetFileName(File), StringComparison.OrdinalIgnoreCase)) return;
                    ui.BeginInvoke(new Action(() => { debounce.Stop(); debounce.Start(); }));
                };
                w.Changed += h; w.Created += h; w.Deleted += h; w.Renamed += (s, e) => h(s, e);
                w.EnableRaisingEvents = true; fsw = w;
            }
            catch { }
        }
    }

    public static class Crash
    {
        public static string Dir { get { return Path.Combine(Program.DataDir, "crashes"); } }
        static int busy;

        // Catches what nothing else caught, writes a report, then the app closes (as it would have anyway).
        public static void Hook()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => { Write(e.ExceptionObject as Exception, "unhandled"); Environment.Exit(1); };   // no Windows error dialog
            TaskScheduler.UnobservedTaskException += (s, e) => { Write(e.Exception, "background task"); e.SetObserved(); };
        }
        public static void HookUi(Application app)
        {
            app.DispatcherUnhandledException += (s, e) =>
            {
                Write(e.Exception, "window");
                e.Handled = true;
                Environment.Exit(1);
            };
        }

        // App, version, Windows version, time, exception chain. Never settings, keys or file contents. Keeps the newest 20.
        public static string Write(Exception ex, string where)
        {
            if (ex == null || System.Threading.Interlocked.Exchange(ref busy, 1) == 1) return null;
            try
            {
                Directory.CreateDirectory(Dir);
                var now = DateTime.Now;
                var path = Path.Combine(Dir, now.ToString("yyyy-MM-dd HH-mm-ss") + ".txt");
                var sb = new StringBuilder();
                sb.AppendLine("Hallow Mixer " + Program.Version + " closed unexpectedly (" + where + ")");
                sb.AppendLine("Time: " + now.ToString("s"));
                sb.AppendLine("Windows: " + WindowsVersion() + (Environment.Is64BitProcess ? " (64-bit)" : ""));
                sb.AppendLine(".NET: " + Environment.Version);
                sb.AppendLine();
                int depth = 0;
                for (var e = ex; e != null && depth < 8; e = e.InnerException, depth++)
                {
                    sb.AppendLine((depth == 0 ? "" : "Inner: ") + e.GetType().FullName + ": " + e.Message);
                    sb.AppendLine(e.StackTrace ?? "(no stack)");
                    sb.AppendLine();
                }
                System.IO.File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                foreach (var old in Directory.GetFiles(Dir, "*.txt").OrderByDescending(f => Path.GetFileName(f)).Skip(20)) try { System.IO.File.Delete(old); } catch { }
                return path;
            }
            catch { return null; }
            finally { busy = 0; }
        }
        static string WindowsVersion()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    return k.GetValue("ProductName") + " " + k.GetValue("DisplayVersion") + " build " + k.GetValue("CurrentBuildNumber") + "." + k.GetValue("UBR");
            }
            catch { return Environment.OSVersion.VersionString; }
        }
        public static string Newest()
        {
            try { return Directory.Exists(Dir) ? Directory.GetFiles(Dir, "*.txt").OrderByDescending(f => Path.GetFileName(f)).FirstOrDefault() : null; } catch { return null; }
        }
        // ISO time of the newest report, or null
        public static string LastTime()
        {
            var f = Newest(); if (f == null) return null;
            DateTime t;
            if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(f), "yyyy-MM-dd HH-mm-ss", null, System.Globalization.DateTimeStyles.None, out t)) return t.ToString("s");
            try { return System.IO.File.GetLastWriteTime(f).ToString("s"); } catch { return null; }
        }
        public static void Open(string file) { try { Process.Start("notepad.exe", "\"" + file + "\""); } catch { } }
        public static void OpenFolder(string file) { try { Process.Start("explorer.exe", "/select,\"" + file + "\""); } catch { } }
    }

    // "Hallow Mixer closed unexpectedly last time" - once per crash report.
    public class CrashWindow : Window
    {
        public CrashWindow(string file)
        {
            Title = "Hallow Mixer"; Width = 460; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
            Background = (Brush)FindResource("Bg"); Foreground = (Brush)FindResource("Text"); FontFamily = new FontFamily("Segoe UI"); FontSize = 12.5;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var sp = new System.Windows.Controls.StackPanel { Margin = new Thickness(22, 18, 22, 16) };
            sp.Children.Add(new System.Windows.Controls.TextBlock { Text = "Hallow Mixer closed unexpectedly last time", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Accent.Brush, TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(new System.Windows.Controls.TextBlock { Text = "A crash report was saved on this PC" + (file != null ? " (" + Path.GetFileName(file) + ")" : "") + ". Nothing is sent anywhere. If it keeps happening, the details help find the cause.",
                Foreground = (Brush)FindResource("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 14) });
            var row = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Func<string, string, Action, System.Windows.Controls.Button> btn = (text, style, click) =>
            {
                var b = new System.Windows.Controls.Button { Content = text, Style = (Style)FindResource(style), Margin = new Thickness(8, 0, 0, 0) };
                b.Click += (s, e) => click(); row.Children.Add(b); return b;
            };
            btn("Show details", "Btn", () => Crash.Open(file));
            btn("Open folder", "Btn", () => Crash.OpenFolder(file));
            btn("OK", "Accent", Close).IsDefault = true;
            sp.Children.Add(row);
            Content = sp;
        }
    }
}
