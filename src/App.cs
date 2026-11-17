// Hallow Mixer - window, tray icon and hotkeys. WPF built in code (C# 5 / .NET Framework 4.8, no installs needed).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using WF = System.Windows.Forms;

[assembly: AssemblyTitle("Hallow Mixer")]
[assembly: AssemblyProduct("Hallow Mixer")]
[assembly: AssemblyDescription("Per-app volume mixer: game, chat, music, media")]
[assembly: AssemblyVersion("3.6.0.0")]
[assembly: AssemblyFileVersion("3.6.0.0")]

namespace HallowMixer
{
    public static class Program
    {
        public const string Version = "3.6";
        public static List<CliCommand> StartupCommands = new List<CliCommand>();
        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Contains("--reset")) { try { new Engine().ResetForUninstall(); } catch { } return; }
            int ai = Array.IndexOf(args, "--after"); int afterPid;
            if (ai >= 0 && ai + 1 < args.Length && int.TryParse(args[ai + 1], out afterPid))
            {
                try { var old = Process.GetProcessById(afterPid); old.WaitForExit(15000); } catch { }   // restarting: let the old copy finish first
                args = args.Where((a, i) => i != ai && i != ai + 1).ToArray();
            }
            List<CliCommand> cmds;
            try { cmds = Cli.Parse(args); }
            catch (ArgumentException ex) { MessageBox.Show(ex.Message, "Hallow Mixer", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            bool created;
            var mutex = new Mutex(true, @"Local\HallowMixer", out created);
            var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\HallowMixerShow");
            if (!created)
            {
                // already running: send it the commands (Stream Deck, shortcuts), or just bring it to the front
                if (cmds.Count > 0 && Cli.Send(cmds)) return;
                if (cmds.Count > 0) MessageBox.Show("Hallow Mixer is running but didn't take the command. Turn on Settings > \"Let Hallow Toolkit and its phone dashboard control the mixer\" - shortcuts use the same local connection.", "Hallow Mixer");
                showEvent.Set(); return;
            }
            StartupCommands = cmds;
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("HallowMixer.Theme.xaml"))
                app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(s));
            var w = new MixerWindow(args.Contains("--tray"));
            var t = new Thread(() => { while (showEvent.WaitOne()) w.Dispatcher.BeginInvoke(new Action(w.ShowFromTray)); }) { IsBackground = true };
            t.Start();
            app.Run();
            GC.KeepAlive(mutex);
        }
    }

    // One column of the mixer.
    class Strip
    {
        public string Id; public Color Color;
        public Slider Fader; public TextBlock Pct, Sub; public ToggleButton Mute; public ComboBox Output;
        public Rectangle Meter; public StackPanel AppList; public double MeterLevel;
    }

    // Small pop-up near the bottom of the screen for hotkeys - can't be clicked and never takes focus from a game.
    public class Osd : Window
    {
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
        readonly TextBlock text = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(0xEC, 0xE8, 0xF4)), FontSize = 15, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
        readonly Border barBack, barFill;
        readonly DispatcherTimer hide = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        public Osd()
        {
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
            ShowInTaskbar = false; ShowActivated = false; SizeToContent = SizeToContent.WidthAndHeight; ResizeMode = ResizeMode.NoResize; Focusable = false;
            barFill = new Border { Background = new SolidColorBrush(Color.FromRgb(0xF0, 0x78, 0x20)), CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, Height = 6, Width = 0 };
            barBack = new Border { Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x32, 0x48)), CornerRadius = new CornerRadius(3), Height = 6, Width = 260, Margin = new Thickness(0, 10, 0, 0), Child = barFill };
            var sp = new StackPanel(); sp.Children.Add(text); sp.Children.Add(barBack);
            Content = new Border { Background = new SolidColorBrush(Color.FromArgb(235, 0x1C, 0x17, 0x24)), CornerRadius = new CornerRadius(10), Padding = new Thickness(22, 14, 22, 14),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x32, 0x48)), BorderThickness = new Thickness(1), Child = sp, MinWidth = 300 };
            hide.Tick += (s, e) => { hide.Stop(); Hide(); };
            SourceInitialized += (s, e) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                SetWindowLong(h, -20, GetWindowLong(h, -20) | 0x20 | 0x80 | 0x08000000);   // click-through, tool window, never activates
            };
        }
        public void Flash(string message, double bar)
        {
            text.Text = message;
            barBack.Visibility = bar >= 0 ? Visibility.Visible : Visibility.Collapsed;
            if (bar >= 0) barFill.Width = 260 * Math.Max(0, Math.Min(1, bar));
            if (!IsVisible) Show();
            UpdateLayout();
            var wa = SystemParameters.WorkArea;
            Left = wa.Left + (wa.Width - ActualWidth) / 2; Top = wa.Bottom - ActualHeight - 90;
            hide.Stop(); hide.Start();
        }
    }

    // Small always-on-top mixer. Its sliders are bound to the main window's faders, so both always match.
    class MiniWindow : Window
    {
        readonly Settings s;
        public MiniWindow(Settings settings, IEnumerable<Tuple<string, Color, Slider>> rows, Slider balance, UIElement extra)
        {
            s = settings;
            Title = "Hallow Mixer - mini"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; SizeToContent = SizeToContent.WidthAndHeight; ResizeMode = ResizeMode.NoResize;
            var sp = new StackPanel { Width = 300 };
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var close = new Button { Content = "x", Style = (Style)FindResource("Btn"), Padding = new Thickness(8, 1, 8, 3), ToolTip = "Close the mini mixer" };
            close.Click += (o, e) => { s.MiniOpen = false; s.Save(); Hide(); };
            DockPanel.SetDock(close, Dock.Right); head.Children.Add(close);
            head.Children.Add(new TextBlock { Text = "HALLOW MIXER", FontWeight = FontWeights.Bold, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0x78, 0x20)), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(head);
            foreach (var r in rows)
            {
                var g = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                var name = new TextBlock { Text = r.Item1, FontSize = 11.5, Foreground = new SolidColorBrush(r.Item2), VerticalAlignment = VerticalAlignment.Center };
                var sl = new Slider { Style = (Style)FindResource("HFader"), Minimum = 0, Maximum = 100, Foreground = new SolidColorBrush(r.Item2), Height = 22 };
                sl.SetBinding(RangeBase.ValueProperty, new System.Windows.Data.Binding("Value") { Source = r.Item3, Mode = System.Windows.Data.BindingMode.TwoWay });
                var pct = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0x88, 0xA0)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                pct.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Value") { Source = r.Item3, StringFormat = "{0:0}%" });
                sl.PreviewMouseWheel += (o, e) => { sl.Value = Math.Max(0, Math.Min(100, Math.Round(sl.Value) + (e.Delta > 0 ? 3 : -3))); e.Handled = true; };
                Grid.SetColumn(sl, 1); Grid.SetColumn(pct, 2);
                g.Children.Add(name); g.Children.Add(sl); g.Children.Add(pct);
                sp.Children.Add(g);
            }
            var bg = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            bg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); bg.ColumnDefinitions.Add(new ColumnDefinition()); bg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            var gl = new TextBlock { Text = "GAME", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0x78, 0x20)), VerticalAlignment = VerticalAlignment.Center };
            var cl = new TextBlock { Text = "CHAT", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0xA9, 0x8B, 0xE0)), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            var bs = new Slider { Style = (Style)FindResource("Balance"), Minimum = -100, Maximum = 100, Height = 22, ToolTip = "Game / chat balance (double-click to centre)" };
            bs.SetBinding(RangeBase.ValueProperty, new System.Windows.Data.Binding("Value") { Source = balance, Mode = System.Windows.Data.BindingMode.TwoWay });
            bs.MouseDoubleClick += (o, e) => { bs.Value = 0; };
            Grid.SetColumn(bs, 1); Grid.SetColumn(cl, 2);
            bg.Children.Add(gl); bg.Children.Add(bs); bg.Children.Add(cl);
            sp.Children.Add(bg);
            if (extra != null) sp.Children.Add(extra);
            var shell = new Border { Background = new SolidColorBrush(Color.FromArgb(240, 0x1C, 0x17, 0x24)), BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x32, 0x48)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 10, 14, 12), Child = sp,
                ToolTip = null };
            shell.MouseLeftButtonDown += (o, e) => { if (e.OriginalSource is Border || e.OriginalSource is TextBlock || e.OriginalSource is StackPanel) { DragMove(); s.MiniLeft = Left; s.MiniTop = Top; s.Save(); } };
            Content = shell;
            if (s.MiniLeft >= 0 && s.MiniTop >= 0 && s.MiniLeft < SystemParameters.VirtualScreenWidth - 50 && s.MiniTop < SystemParameters.VirtualScreenHeight - 50) { Left = s.MiniLeft; Top = s.MiniTop; }
            else { var wa = SystemParameters.WorkArea; Left = wa.Right - 340; Top = wa.Bottom - 300; }
        }
    }

    public class MixerWindow : Window
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);

        // ---- push-to-talk / push-to-mute: reads the key state directly (no keyboard hook, so anti-cheat friendly)
        DateTime pttRelease; bool pttActive; int captureKey;   // captureKey: 0 = not waiting, 1 = waiting for a key press
        TextBlock pttKeyText; Button pttKeyBtn;
        static string KeyName(int vk)
        {
            if (vk == 4) return "Middle mouse button"; if (vk == 5) return "Mouse back button"; if (vk == 6) return "Mouse forward button";
            var k = KeyInterop.KeyFromVirtualKey(vk); return k == Key.None ? "Key " + vk : k.ToString();
        }
        void TickPtt()
        {
            if (captureKey == 1)
            {
                if ((GetAsyncKeyState(0x1B) & 0x8000) != 0) { captureKey = 0; pttKeyBtn.Content = "Change"; return; }   // Esc = cancel
                for (int vk = 3; vk < 255; vk++)
                {
                    if (vk == 0x1B || vk == 0x10 || vk == 0x11 || vk == 0x12) continue;   // Esc and the plain Shift/Ctrl/Alt codes
                    if ((GetAsyncKeyState(vk) & 0x8000) != 0)
                    {
                        eng.S.PttKey = vk; eng.S.Save(); captureKey = 0;
                        pttKeyBtn.Content = "Change"; pttKeyText.Text = KeyName(vk); Status("Push-to-talk key: " + KeyName(vk)); return;
                    }
                }
                return;
            }
            var mode = eng.S.MicMode ?? "off";
            if (mode == "off") { if (pttActive) { pttActive = false; SetMicMute(false); } return; }
            pttActive = true;
            bool held = (GetAsyncKeyState(eng.S.PttKey) & 0x8000) != 0;
            if (held) pttRelease = DateTime.UtcNow.AddMilliseconds(250);   // small tail so your last word isn't cut off
            bool wantMute = mode == "ptt" ? DateTime.UtcNow >= pttRelease : held;
            if (wantMute != (mic.Mute.IsChecked == true)) SetMicMute(wantMute);
        }
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

        // Ctrl+Alt+Up/Down: volume of the app you're in (its own slider inside its channel)
        void FrontAppVolume(int step)
        {
            uint pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid);
            var a = eng.AppForPid(pid);
            if (a == null)
            {
                var k = eng.KeyForPid(pid);
                Notice(k == null ? "Can't tell which app is in front" : "No sound from " + k + " right now", -1); return;
            }
            int v = Math.Max(0, Math.Min(100, eng.AppVolume(a.Key) + step));
            if (v == 100) eng.S.AppVolume.Remove(a.Key); else eng.S.AppVolume[a.Key] = v;
            eng.Apply(); dirty = true; RebuildApps();
            Notice(a.Name + "  " + v + "%", v / 100.0);
        }

        static readonly Dictionary<string, Color> Colors = new Dictionary<string, Color> {
            { "game", C("#F07820") }, { "chat", C("#A98BE0") }, { "music", C("#6ED28C") }, { "media", C("#5AB4F0") }, { "other", C("#8F88A0") }, { "mic", C("#F05A8C") } };
        static Color C(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
        static SolidColorBrush B(string hex) { return new SolidColorBrush(C(hex)); }
        static SolidColorBrush B(Color c) { return new SolidColorBrush(c); }

        readonly Engine eng = new Engine();
        readonly Dictionary<string, Strip> strips = new Dictionary<string, Strip>();
        Strip mic; Slider master, balance; ToggleButton masterMute; ComboBox outputPick; Rectangle masterMeter; TextBlock balanceText, status;
        WF.NotifyIcon tray; WF.ToolStripMenuItem trayMicMute, trayStartup;
        bool loading, exiting, dirty; readonly bool startHidden;
        DispatcherTimer meterTimer, refreshTimer, saveTimer;

        public MixerWindow(bool hidden)
        {
            startHidden = hidden;
            Title = "Hallow Mixer"; Width = 1010; Height = 690; MinWidth = 900; MinHeight = 600;
            Background = (Brush)FindResource("Bg"); Foreground = (Brush)FindResource("Text");
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12.5;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            try { Icon = BitmapFromIcon(); } catch { }
            Content = Build();
            SourceInitialized += (s, e) => OnHandle();
            Closing += (s, e) => { if (!exiting) { e.Cancel = true; HideToTray(); } };
            // remember size and place (only if it's still on a screen)
            var vs = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            if (eng.S.WinWidth >= MinWidth && eng.S.WinHeight >= MinHeight && vs.Contains(new Point(eng.S.WinLeft + 60, eng.S.WinTop + 30)))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = eng.S.WinLeft; Top = eng.S.WinTop; Width = eng.S.WinWidth; Height = eng.S.WinHeight;
            }
            if (eng.S.WinMax) WindowState = WindowState.Maximized;
            EventHandler placeChanged = (s, e) =>
            {
                if (!IsLoaded) return;
                eng.S.WinMax = WindowState == WindowState.Maximized;
                if (WindowState == WindowState.Normal) { eng.S.WinLeft = Left; eng.S.WinTop = Top; eng.S.WinWidth = ActualWidth; eng.S.WinHeight = ActualHeight; }
                dirty = true;
            };
            LocationChanged += placeChanged; StateChanged += placeChanged; SizeChanged += (s, e) => placeChanged(s, EventArgs.Empty);
            SetupTray();
            eng.Refresh(); eng.LevelsSwitched = false; RebuildApps(); LoadValues(); UpdateDefaults();
            meterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) }; meterTimer.Tick += (s, e) => TickMeters(); meterTimer.Start();
            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; refreshTimer.Tick += (s, e) =>
            {
                CheckAutoSwitch();
                UpdateDefaults();
                KeepMicLevel();
                TickQuiet();
                if (eng.S.NowPlayingOn) NowPlaying.Poll(m => Dispatcher.BeginInvoke(new Action(() => ShowNow(m))));
                if (++cleanTick % 5 == 0 && eng.S.ChatClean && eng.CheckChatClean(CoreAudio.Devices(0)))
                { LoadValues(); UpdateCleanButton(); Status("NVIDIA Broadcast's speaker is gone, so Chat is back on its normal output."); }
                if (eng.S.ObsAuto && !obs.Connected && !obsConnecting && (DateTime.Now - lastObsTry).TotalSeconds > 30) { var t = ConnectObs(true); }
                if (eng.Refresh()) RebuildApps(); else UpdateSubs();
                if (eng.LevelsSwitched) { eng.LevelsSwitched = false; LoadValues(); Status(eng.LevelsNote); if (eng.S.Overlay) osd.Flash(eng.LevelsNote, -1); }
                if (eng.EqSwitched) { eng.EqSwitched = false; if (eqWin != null && eqWin.IsLoaded) eqWin.Reload(); }
                if (gamesWin != null && gamesWin.IsLoaded && eng.CurrentGame != shownGame) { shownGame = eng.CurrentGame; gamesWin.Fill(); }
            };
            refreshTimer.Start();
            if (eng.S.ApiOn) StartApi();
            foreach (var c in Program.StartupCommands) { try { ApiDo(c.Path, c.Query); } catch { } }   // started by a shortcut with commands
            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; saveTimer.Tick += (s, e) => { if (dirty) { dirty = false; eng.S.Save(); } }; saveTimer.Start();
            if (!startHidden) Show(); else { WindowState = WindowState.Minimized; Show(); Hide(); WindowState = WindowState.Normal; }
            if (eng.S.MiniOpen) { eng.S.MiniOpen = false; ToggleMini(); }
            UpdateNoiseButton(); UpdateCleanButton();
            if (Settings.FirstRun && !eng.S.SetupDone && !startHidden) Dispatcher.BeginInvoke(new Action(OpenSetup), DispatcherPriority.ApplicationIdle);
            DateTime lastCheck;
            if (eng.S.AutoUpdate && (!DateTime.TryParse(eng.S.LastUpdateCheck, out lastCheck) || (DateTime.Now - lastCheck).TotalHours > 20)) CheckForUpdate(false);
        }

        static System.Windows.Media.ImageSource BitmapFromIcon()
        {
            var ico = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            return Imaging.CreateBitmapSourceFromHIcon(ico.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        }

        // ================================================================ layout
        UIElement Build()
        {
            var root = new Grid { Margin = new Thickness(18, 12, 18, 12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ---- header: name + master output
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 12), LastChildFill = false };
            var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            title.Children.Add(new TextBlock { Text = "HALLOW", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = B("#F07820") });
            title.Children.Add(new TextBlock { Text = "  MIXER", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = B("#A98BE0"), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 5) });
            DockPanel.SetDock(title, Dock.Left); head.Children.Add(title);
            var mp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var gear = new ToggleButton { Content = "Settings", Style = (Style)FindResource("Btn"), Margin = new Thickness(0, 0, 16, 0) };
            gear.Checked += (s, e) => { settingsPanel.Visibility = Visibility.Visible; };
            gear.Unchecked += (s, e) => { settingsPanel.Visibility = Visibility.Collapsed; };
            var miniBtn = new Button { Content = "Mini", Style = (Style)FindResource("Btn"), Margin = new Thickness(0, 0, 8, 0), ToolTip = "Small always-on-top mixer" };
            miniBtn.Click += (s, e) => ToggleMini();
            updateBtn = new Button { Style = (Style)FindResource("Accent"), Margin = new Thickness(0, 0, 8, 0), Visibility = Visibility.Collapsed };
            updateBtn.Click += (s, e) => InstallUpdate();
            mp.Children.Add(updateBtn);
            mp.Children.Add(miniBtn);
            var eqBtn = new Button { Content = "EQ", Style = (Style)FindResource("Btn"), Margin = new Thickness(0, 0, 8, 0), ToolTip = "Equalizer for each headset, speaker and mic" };
            eqBtn.Click += (s, e) =>
            {
                if (eqWin == null || !eqWin.IsLoaded) { eqWin = new EqWindow(eng.S) { Owner = this, OnChanged = eng.NoteEqChanged }; eqWin.Closed += (o, a) => { eng.S.Save(); }; eqWin.Show(); }
                else eqWin.Activate();
            };
            mp.Children.Add(eqBtn);
            var streamBtn = new Button { Content = "Stream", Style = (Style)FindResource("Btn"), Margin = new Thickness(0, 0, 8, 0), ToolTip = "OBS: a separate mix for your stream, and presets per scene" };
            streamBtn.Click += (s, e) =>
            {
                if (streamWin == null || !streamWin.IsLoaded) { streamWin = new StreamWindow(eng, obs, ConnectObs) { Owner = this }; streamWin.Closed += (o, a) => eng.S.Save(); streamWin.Show(); }
                else streamWin.Activate();
            };
            mp.Children.Add(streamBtn);
            var gamesBtn = new Button { Content = "Games", Style = (Style)FindResource("Btn"), Margin = new Thickness(0, 0, 8, 0), ToolTip = "Each game's saved levels and EQ" };
            gamesBtn.Click += (s, e) =>
            {
                if (gamesWin == null || !gamesWin.IsLoaded) { gamesWin = new GamesWindow(eng, () => { LoadValues(); if (eqWin != null && eqWin.IsLoaded) eqWin.Reload(); }) { Owner = this }; gamesWin.Show(); }
                else { gamesWin.Fill(); gamesWin.Activate(); }
            };
            mp.Children.Add(gamesBtn);
            mp.Children.Add(gear);
            mp.Children.Add(Label("Output", 0, 8));
            outputPick = new ComboBox { Width = 250, Style = (Style)FindResource("Combo"), ToolTip = "Windows' default output - where apps play unless a channel sends them somewhere else" };
            outputPick.DropDownOpened += (s, e) => FillDevices(outputPick, 0, null, false);
            outputPick.SelectionChanged += (s, e) => { if (loading) return; var d = outputPick.SelectedItem as AudioDevice; if (d != null) { CoreAudio.SetDefault(d.Id); LoadMaster(); } };
            mp.Children.Add(outputPick);
            masterMeter = new Rectangle { Width = 0, Height = 4, Fill = B("#F07820"), HorizontalAlignment = HorizontalAlignment.Left, RadiusX = 2, RadiusY = 2 };
            var ms = new Grid { Width = 190, Margin = new Thickness(14, 0, 8, 0) };
            master = new Slider { Style = (Style)FindResource("HFader"), Minimum = 0, Maximum = 100, Foreground = B("#ECE8F4"), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Master volume" };
            master.ValueChanged += (s, e) => { if (!loading) { var id = CoreAudio.DefaultId(0); if (id != null) CoreAudio.SetLevel(id, (float)(master.Value / 100)); } };
            ms.Children.Add(master);
            var mm = new Border { Height = 4, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, -6), Child = masterMeter };
            ms.Children.Add(mm);
            mp.Children.Add(ms);
            masterMute = new ToggleButton { Content = "Mute", Style = (Style)FindResource("Toggle"), Width = 64 };
            masterMute.Click += (s, e) => { var id = CoreAudio.DefaultId(0); if (id != null) CoreAudio.SetMute(id, masterMute.IsChecked == true); masterMute.Content = masterMute.IsChecked == true ? "Muted" : "Mute"; };
            mp.Children.Add(masterMute);
            DockPanel.SetDock(mp, Dock.Right); head.Children.Add(mp);
            Grid.SetRow(head, 0); root.Children.Add(head);

            // ---- channel strips + mic
            var row = new UniformGrid { Rows = 1 };
            foreach (var id in Engine.ChannelIds) row.Children.Add(BuildStrip(id));
            row.Children.Add(BuildMic());
            Grid.SetRow(row, 1); root.Children.Add(row);

            // ---- game / chat balance + presets
            var bar = new Border { Background = (Brush)FindResource("Panel"), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 10, 16, 10), Margin = new Thickness(5, 10, 5, 0) };
            var bg = new DockPanel();
            var bl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            bl.Children.Add(new TextBlock { Text = "GAME", FontWeight = FontWeights.Bold, Foreground = B("#F07820"), VerticalAlignment = VerticalAlignment.Center });
            balance = new Slider { Style = (Style)FindResource("Balance"), Minimum = -100, Maximum = 100, Width = 300, Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Game / chat balance. Drag toward CHAT to turn the game down while you talk. Ctrl+Alt+PageUp / PageDown. Double-click to centre." };
            balance.ValueChanged += (s, e) => { if (loading) return; eng.S.ChatMix = (int)Math.Round(balance.Value); eng.Apply(); eng.NoteLevelsChanged(); dirty = true; UpdateBalanceText(); };
            balance.MouseDoubleClick += (s, e) => { balance.Value = 0; };
            bl.Children.Add(balance);
            bl.Children.Add(new TextBlock { Text = "CHAT", FontWeight = FontWeights.Bold, Foreground = B("#A98BE0"), VerticalAlignment = VerticalAlignment.Center });
            balanceText = new TextBlock { Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Width = 170 };
            bl.Children.Add(balanceText);
            DockPanel.SetDock(bl, Dock.Left); bg.Children.Add(bl);
            presetBar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            RebuildPresets();
            bg.Children.Add(presetBar);
            bar.Child = bg;
            Grid.SetRow(bar, 2); root.Children.Add(bar);

            // ---- status line
            status = new TextBlock { Foreground = (Brush)FindResource("Muted"), FontSize = 11.5, Margin = new Thickness(6, 10, 6, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetRow(status, 4); root.Children.Add(status);
            nowBar = new NowBar(false) { Margin = new Thickness(5, 10, 5, 0), Command = MediaCommand };
            Grid.SetRow(nowBar, 3); root.Children.Add(nowBar);
            Status(CoreAudio.CanRoutePerApp ? "Right-click an app to move it, click its icon to mute it, scroll over a channel to change it.  Ctrl+Alt+PageUp/PageDown = game/chat,  Ctrl+Alt+M = mute mic."
                                            : "Per-channel output isn't supported on this Windows version - volumes still work.");
            var outer = new Grid();
            outer.Children.Add(root);
            settingsPanel = BuildSettings();
            outer.Children.Add(settingsPanel);
            return outer;
        }

        // ---- settings flyout (top right)
        Border settingsPanel;
        Border BuildSettings()
        {
            var b = new Border { Background = B("#241E2E"), BorderBrush = B("#3A3248"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18, 14, 18, 14), Width = 400, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 58, 18, 18), Visibility = Visibility.Collapsed };
            b.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, Opacity = 0.5, ShadowDepth = 4 };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = "Settings", FontWeight = FontWeights.Bold, FontSize = 15, Margin = new Thickness(0, 0, 0, 8) });
            var pr = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var prl = new TextBlock { Text = "Profile", Width = 52, VerticalAlignment = VerticalAlignment.Center };
            profileBox = new ComboBox { Style = (Style)FindResource("Combo"), Width = 150, ToolTip = "A whole mixer setup - levels, presets, app choices, EQ and switches. Switching restarts Hallow Mixer for a moment." };
            System.Windows.Automation.AutomationProperties.SetName(profileBox, "Profile");
            profileBox.SelectionChanged += (s, e) => { if (loading) return; var n = profileBox.SelectedItem as string; if (n != null) UseProfile(n, false); };
            var prNew = new Button { Content = "New...", Style = (Style)FindResource("Btn"), Margin = new Thickness(8, 0, 0, 0), ToolTip = "Start a new profile from your current setup" };
            prNew.Click += (s, e) => NewProfile();
            profileDel = new Button { Content = "Delete", Style = (Style)FindResource("Btn"), Margin = new Thickness(6, 0, 0, 0), ToolTip = "Delete the profile you're using and switch to another one" };
            profileDel.Click += (s, e) =>
            {
                var cur = eng.S.ProfileName; var others = Profiles.List(cur).Where(x => !string.Equals(x, cur, StringComparison.OrdinalIgnoreCase)).ToList();
                if (others.Count == 0) return;
                if (MessageBox.Show(this, "Delete the \"" + cur + "\" profile and switch to \"" + others[0] + "\"?\n\nIts levels, presets, app choices and EQ are deleted (this can't be undone).", "Hallow Mixer", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                try { File.Copy(System.IO.Path.Combine(Profiles.Dir, others[0] + ".json"), Settings.FilePathPublic, true); Profiles.Delete(cur); Restart(); }
                catch (Exception ex) { Status("Couldn't delete the profile: " + ex.Message); }
            };
            DockPanel.SetDock(prl, Dock.Left); DockPanel.SetDock(profileBox, Dock.Left); DockPanel.SetDock(prNew, Dock.Left);
            pr.Children.Add(prl); pr.Children.Add(profileBox); pr.Children.Add(prNew); pr.Children.Add(profileDel);
            sp.Children.Add(pr);
            Dispatcher.BeginInvoke(new Action(RebuildProfiles));
            Func<string, bool, Action<bool>, CheckBox> sw = (text, on, set) =>
            {
                var c = new CheckBox { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, IsChecked = on, Style = (Style)FindResource("Switch") };
                c.Click += (s, e) => { set(c.IsChecked == true); eng.S.Save(); eng.Apply(); };
                sp.Children.Add(c); return c;
            };
            sw("Turn game, music and media down when someone talks in Chat", eng.S.DuckOnChat, v => eng.S.DuckOnChat = v);
            sw("Turn music and media down when I talk", eng.S.DuckOnMic, v => eng.S.DuckOnMic = v);
            var dr = new DockPanel { Margin = new Thickness(52, 2, 0, 8) };
            var dl = new TextBlock { Width = 110, Foreground = (Brush)FindResource("Muted"), VerticalAlignment = VerticalAlignment.Center };
            var ds = new Slider { Style = (Style)FindResource("HFader"), Minimum = 10, Maximum = 90, Value = eng.S.DuckAmount, Foreground = B("#F07820"), VerticalAlignment = VerticalAlignment.Center };
            Action dtext = () => { dl.Text = "Turn down by " + eng.S.DuckAmount + "%"; };
            ds.ValueChanged += (s, e) => { eng.S.DuckAmount = (int)Math.Round(ds.Value / 5) * 5; dtext(); dirty = true; };
            dtext(); DockPanel.SetDock(dl, Dock.Left); dr.Children.Add(dl); dr.Children.Add(ds); sp.Children.Add(dr);
            var smart = sw("Only for real talking (ignore join/leave sounds, pings and short noises)", eng.S.SmartDuck, v => eng.S.SmartDuck = v);
            smart.Margin = new Thickness(52, 0, 0, 6);
            sw("Remember my levels for each game (they come back when the game starts)", eng.S.PerGame, v => eng.S.PerGame = v);
            sw("EQ follows the game (each game remembers its headset EQ)", eng.S.EqPerGame, v => eng.S.EqPerGame = v);
            sw("Even out Media: loud ads and explosions down, quiet talking up", eng.S.LevelMedia, v => eng.S.LevelMedia = v);
            sw("Loud-sound guard: sudden blasts in any channel (a flashbang, a scream, a mic squeal in Chat) are turned down for a moment", eng.S.Guard, v => eng.S.Guard = v);
            sw("Show a pop-up on screen when I use the hotkeys", eng.S.Overlay, v => eng.S.Overlay = v);
            sw("Preset hotkeys: Ctrl+Alt+1 to 9 for your presets, Ctrl+Alt+0 for the next one", eng.S.PresetKeys, v => { eng.S.PresetKeys = v; RegisterPresetKeys(v); });
            sw("Show what's playing (song or video, with play/pause and skip)", eng.S.NowPlayingOn, v => { eng.S.NowPlayingOn = v; ShowNow(v ? NowPlaying.Current : null); });
            sw("Headset first: use it whenever it's on, otherwise use the fallback", eng.S.AutoSwitch, v => { eng.S.AutoSwitch = v; lastAuto = null; });
            var ag = new Grid { Margin = new Thickness(52, 2, 0, 6) };
            ag.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) }); ag.ColumnDefinitions.Add(new ColumnDefinition());
            ag.RowDefinitions.Add(new RowDefinition()); ag.RowDefinitions.Add(new RowDefinition());
            Func<string, int, Func<string>, Action<string>, ComboBox> pick = (label, r, get, set) =>
            {
                var l = Label(label, 0, 6); Grid.SetRow(l, r); ag.Children.Add(l);
                var c = new ComboBox { Style = (Style)FindResource("Combo"), Margin = new Thickness(0, 3, 0, 3) };
                c.DropDownOpened += (s, e) => FillDevices(c, 0, get(), false);
                c.SelectionChanged += (s, e) => { if (loading) return; var d = c.SelectedItem as AudioDevice; if (d != null) { set(d.Id); eng.S.Save(); lastAuto = null; } };
                Grid.SetRow(c, r); Grid.SetColumn(c, 1); ag.Children.Add(c);
                FillDevices(c, 0, get(), false);
                if (string.IsNullOrEmpty(get())) c.SelectedItem = null;
                return c;
            };
            pick("Headset", 0, () => eng.S.PrimaryOut, v => eng.S.PrimaryOut = v);
            pick("Fallback", 1, () => eng.S.FallbackOut, v => eng.S.FallbackOut = v);
            sp.Children.Add(ag);
            var pm = new DockPanel { Margin = new Thickness(0, 8, 0, 4) };
            var pml = new TextBlock { Text = "Mic", Width = 52, VerticalAlignment = VerticalAlignment.Center };
            var mode = new ComboBox { Style = (Style)FindResource("Combo"), Width = 150 };
            mode.ItemsSource = new[] { "Always on", "Push to talk", "Push to mute" };
            mode.SelectedIndex = eng.S.MicMode == "ptt" ? 1 : eng.S.MicMode == "ptm" ? 2 : 0;
            mode.SelectionChanged += (s, e) => { eng.S.MicMode = mode.SelectedIndex == 1 ? "ptt" : mode.SelectedIndex == 2 ? "ptm" : "off"; eng.S.Save();
                Status(mode.SelectedIndex == 1 ? "Push to talk: your mic is muted until you hold " + KeyName(eng.S.PttKey) + "." : mode.SelectedIndex == 2 ? "Push to mute: hold " + KeyName(eng.S.PttKey) + " to mute your mic." : "Mic always on."); };
            pttKeyText = new TextBlock { Text = KeyName(eng.S.PttKey), Foreground = (Brush)FindResource("Muted"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 8, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            pttKeyBtn = new Button { Content = "Change", Style = (Style)FindResource("Btn"), ToolTip = "Then press the key or mouse button you want (Esc = cancel)" };
            pttKeyBtn.Click += (s, e) => { captureKey = 0; pttKeyBtn.Content = "Press a key..."; Dispatcher.BeginInvoke(new Action(() => { captureKey = 1; }), DispatcherPriority.Background); };
            DockPanel.SetDock(pml, Dock.Left); DockPanel.SetDock(mode, Dock.Left); DockPanel.SetDock(pttKeyBtn, Dock.Right);
            pm.Children.Add(pml); pm.Children.Add(mode); pm.Children.Add(pttKeyBtn); pm.Children.Add(pttKeyText);
            sp.Children.Add(pm);
            sw("Tell me when I talk while my mic is muted (pop-up on screen)", eng.S.MutedWarn, v => eng.S.MutedWarn = v);
            sw("Quiet hours: cap the volume and turn Windows sounds down at night", eng.S.QuietOn, v => { eng.S.QuietOn = v; TickQuiet(); });
            var qr = new WrapPanel { Margin = new Thickness(52, 0, 0, 8) };
            Func<string, IEnumerable<string>, int, Action<int>, ComboBox> qc = (label, items, sel, set) =>
            {
                qr.Children.Add(new TextBlock { Text = label, Foreground = (Brush)FindResource("Muted"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
                var c = new ComboBox { Style = (Style)FindResource("Combo"), Width = 72, Margin = new Thickness(0, 0, 10, 0), ItemsSource = items.ToList(), SelectedIndex = sel };
                System.Windows.Automation.AutomationProperties.SetName(c, "Quiet hours " + label);
                c.SelectionChanged += (s, e) => { if (c.SelectedIndex >= 0) { set(c.SelectedIndex); eng.S.Save(); TickQuiet(); } };
                qr.Children.Add(c); return c;
            };
            var hours = Enumerable.Range(0, 24).Select(h => h.ToString("00") + ":00");
            var caps = new[] { 20, 30, 40, 50, 60, 70 };
            qc("from", hours, eng.S.QuietFrom, i => eng.S.QuietFrom = i);
            qc("to", hours, eng.S.QuietTo, i => eng.S.QuietTo = i);
            qc("max", caps.Select(c => c + "%"), Math.Max(0, Array.IndexOf(caps, eng.S.QuietMax)), i => eng.S.QuietMax = caps[i]);
            sp.Children.Add(qr);
            micKeepSwitch = sw("Keep my mic level (puts it back if Discord, a game or Windows changes it)", eng.S.MicKeep, v =>
            {
                eng.S.MicKeep = v; keptMic = null;
                if (v) { var id = CoreAudio.DefaultId(1); var l = id == null ? -1 : CoreAudio.GetLevel(id); eng.S.MicKeepLevel = l < 0 ? -1 : (int)Math.Round(l * 100); keptMic = id;
                    Status("Your mic stays at " + eng.S.MicKeepLevel + "%. Move the Mic fader to change it."); }
            });
            var st = sw("Start with Windows (in the tray)", StartsWithWindows(), v => { SetStartWithWindows(v); if (trayStartup != null) trayStartup.Checked = v; });
            var sc = new Button { Content = "Shortcuts for Stream Deck...", Style = (Style)FindResource("Btn"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0),
                ToolTip = "Copy command lines for Stream Deck's Open action, AutoHotkey or desktop shortcuts" };
            sc.Click += (s, e) =>
            {
                var exe = Assembly.GetExecutingAssembly().Location;
                var lines = new[] { "--preset " + (eng.S.Presets.Count > 0 ? "\"" + eng.S.Presets[0].Name + "\"" : "Gaming"), "--toggle-mic", "--channel game 60", "--channel chat +10", "--toggle-mute music", "--media playpause" };
                var text = "Hallow Mixer shortcuts - use one line as a Stream Deck \"Open\" action, a desktop shortcut target or in AutoHotkey:\r\n\r\n" +
                    string.Join("\r\n", lines.Select(l => "\"" + exe + "\" " + l)) +
                    "\r\n\r\nChannels: game, chat, music, media, system. Levels 0-100, or +10 / -10.\r\nMic: --mute-mic, --unmute-mic, --toggle-mic.  Channels: --mute, --unmute, --toggle-mute.  Media: --media playpause | next | prev.";
                try { Clipboard.SetText(text); Status("Shortcut examples copied - paste them into Notepad to pick one."); } catch { Status("Couldn't use the clipboard - try again."); }
            };
            sp.Children.Add(sc);
            var again = new Button { Content = "Run setup again", Style = (Style)FindResource("Btn"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0),
                ToolTip = "With Sonar, replacing Sonar, or just a mixer - sets channels, outputs and mic for you" };
            again.Click += (s, e) => OpenSetup();
            sp.Children.Add(again);
            var games = new Button { Content = "Forget saved game levels", Style = (Style)FindResource("Btn"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0) };
            games.Click += (s, e) => { int n = eng.S.GameLevels.Count; eng.S.GameLevels.Clear(); eng.S.Save(); Status("Forgot the saved levels for " + n + " game(s)."); };
            sp.Children.Add(games);
            sw("Check for updates when Hallow Mixer starts (asks before installing)", eng.S.AutoUpdate, v => eng.S.AutoUpdate = v);
            sw("Let Hallow Toolkit and its phone dashboard control the mixer (this PC only)", eng.S.ApiOn, v => { eng.S.ApiOn = v; if (v) StartApi(); else StopApi(); });
            var ur = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            var checkNow = new Button { Content = "Check for updates now", Style = (Style)FindResource("Btn"), Margin = new Thickness(0, 0, 8, 6) };
            checkNow.Click += (s, e) => CheckForUpdate(true);
            var exp = new Button { Content = "Back up settings...", Style = (Style)FindResource("Btn"), Margin = new Thickness(0, 0, 8, 6), ToolTip = "Save all levels, presets, EQ and app choices to a file" };
            exp.Click += (s, e) =>
            {
                var d = new SaveFileDialog { Filter = "Hallow Mixer settings (*.json)|*.json", FileName = "Hallow Mixer settings.json", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) };
                if (d.ShowDialog(this) != true) return;
                eng.S.Save(); File.Copy(Settings.FilePathPublic, d.FileName, true); Status("Settings saved to " + d.FileName);
            };
            var imp = new Button { Content = "Restore settings...", Style = (Style)FindResource("Btn"), Margin = new Thickness(0, 0, 8, 6), ToolTip = "Load a settings file you backed up (Hallow Mixer restarts)" };
            imp.Click += (s, e) =>
            {
                var d = new OpenFileDialog { Filter = "Hallow Mixer settings (*.json)|*.json", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) };
                if (d.ShowDialog(this) != true) return;
                if (!Settings.LooksValid(d.FileName)) { Status("That file isn't a Hallow Mixer settings file."); return; }
                File.Copy(d.FileName, Settings.FilePathPublic, true);
                Restart();
            };
            ur.Children.Add(checkNow); ur.Children.Add(exp); ur.Children.Add(imp);
            sp.Children.Add(ur);
            sp.Children.Add(new TextBlock { Text = "Hallow Mixer v" + Program.Version, Foreground = (Brush)FindResource("Muted"), FontSize = 11, Margin = new Thickness(0, 12, 0, 0) });
            b.Child = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            return b;
        }

        static TextBlock Label(string t, double left, double right)
        { return new TextBlock { Text = t, Foreground = new SolidColorBrush(C("#8F88A0")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(left, 0, right, 0) }; }

        Border StripShell(Strip st, string name)
        {
            var shell = new Border { Background = (Brush)FindResource("Panel"), CornerRadius = new CornerRadius(8), Margin = new Thickness(5, 0, 5, 0), Padding = new Thickness(12, 12, 12, 10) };
            var top = new Border { Height = 3, Background = B(st.Color), CornerRadius = new CornerRadius(2), Margin = new Thickness(-12, -12, -12, 10) };
            var sp = new DockPanel();
            var hdr = new StackPanel(); DockPanel.SetDock(hdr, Dock.Top);
            hdr.Children.Add(top);
            hdr.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.Bold, FontSize = 14 });
            st.Sub = new TextBlock { Foreground = (Brush)FindResource("Muted"), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
            hdr.Children.Add(st.Sub);
            sp.Children.Add(hdr);
            shell.Child = sp;
            return shell;
        }

        // fader with a level meter beside it, then the % label
        FrameworkElement FaderBlock(Strip st, string tip)
        {
            var g = new Grid { Height = 210, Margin = new Thickness(0, 12, 0, 6), HorizontalAlignment = HorizontalAlignment.Center };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            st.Fader = new Slider { Style = (Style)FindResource("VFader"), Foreground = B(st.Color), ToolTip = tip };
            Grid.SetColumn(st.Fader, 0); g.Children.Add(st.Fader);
            var track = new Border { Background = B("#2A2333"), CornerRadius = new CornerRadius(3) };
            st.Meter = new Rectangle { Fill = B(st.Color), VerticalAlignment = VerticalAlignment.Bottom, Height = 0, RadiusX = 3, RadiusY = 3, Opacity = 0.85 };
            var mg = new Grid(); mg.Children.Add(track); mg.Children.Add(st.Meter);
            Grid.SetColumn(mg, 2); g.Children.Add(mg);
            return g;
        }

        UIElement BuildStrip(string id)
        {
            var st = new Strip { Id = id, Color = Colors[id] };
            strips[id] = st;
            var shell = StripShell(st, Engine.ChannelNames[id]);
            var dp = (DockPanel)shell.Child;
            var body = new StackPanel { Background = Brushes.Transparent }; DockPanel.SetDock(body, Dock.Top);
            body.Children.Add(FaderBlock(st, "Volume of every app in " + Engine.ChannelNames[id] + " (scroll to change)"));
            st.Fader.ValueChanged += (s, e) => { if (loading) return; eng.Chan(id).Volume = (int)Math.Round(st.Fader.Value); st.Pct.Text = (int)st.Fader.Value + "%"; eng.Apply(); eng.NoteLevelsChanged(); dirty = true; };
            body.PreviewMouseWheel += (s, e) => { st.Fader.Value = Math.Max(0, Math.Min(100, Math.Round(st.Fader.Value) + (e.Delta > 0 ? 3 : -3))); e.Handled = true; };
            st.Pct = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
            body.Children.Add(st.Pct);
            st.Mute = new ToggleButton { Content = "Mute", Style = (Style)FindResource("Toggle"), Margin = new Thickness(0, 0, 0, 8) };
            st.Mute.Click += (s, e) => { var m = st.Mute.IsChecked == true; eng.Chan(id).Muted = m; st.Mute.Content = m ? "Muted" : "Mute"; eng.Apply(); dirty = true; };
            body.Children.Add(st.Mute);
            st.Output = new ComboBox { Style = (Style)FindResource("Combo"), Margin = new Thickness(0, 0, 0, 10), IsEnabled = CoreAudio.CanRoutePerApp,
                ToolTip = "Where this channel's apps play. 'Default output' follows Windows." };
            st.Output.DropDownOpened += (s, e) => FillDevices(st.Output, 0, eng.Chan(id).OutputId, true);
            st.Output.SelectionChanged += (s, e) =>
            {
                if (loading) return;
                var d = st.Output.SelectedItem as AudioDevice; eng.Chan(id).OutputId = d == null ? "" : d.Id; eng.Apply(); dirty = true;
                Status(Engine.ChannelNames[id] + " apps now play on " + (d == null || d.Id == "" ? "the default output" : d.Name) + ". Some apps switch after they restart.");
            };
            body.Children.Add(st.Output);
            if (id == "chat")
            {
                cleanBtn = new ToggleButton { Style = (Style)FindResource("Toggle"), Margin = new Thickness(0, 0, 0, 10), Visibility = Visibility.Collapsed };
                cleanBtn.Click += (s, e) => CleanChat();
                body.Children.Add(cleanBtn);
            }
            dp.Children.Add(body);
            st.AppList = new StackPanel();
            var sv = new ScrollViewer { Content = st.AppList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            dp.Children.Add(sv);
            return shell;
        }

        UIElement BuildMic()
        {
            var st = new Strip { Id = "mic", Color = Colors["mic"] };
            mic = st;
            var shell = StripShell(st, "Mic");
            var dp = (DockPanel)shell.Child;
            var body = new StackPanel { Background = Brushes.Transparent }; DockPanel.SetDock(body, Dock.Top);
            body.Children.Add(FaderBlock(st, "Your mic level (what games and Discord get). Using Sonar? Set your real mic's level in Sonar."));
            body.PreviewMouseWheel += (s, e) => { st.Fader.Value = Math.Max(0, Math.Min(100, Math.Round(st.Fader.Value) + (e.Delta > 0 ? 3 : -3))); e.Handled = true; };
            st.Fader.ValueChanged += (s, e) => { if (loading) return; var mid = CoreAudio.DefaultId(1); if (mid != null) CoreAudio.SetLevel(mid, (float)(st.Fader.Value / 100)); st.Pct.Text = (int)st.Fader.Value + "%";
                if (eng.S.MicKeep) { eng.S.MicKeepLevel = (int)Math.Round(st.Fader.Value); keptMic = mid; dirty = true; } };
            st.Pct = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
            body.Children.Add(st.Pct);
            st.Mute = new ToggleButton { Content = "Mute", Style = (Style)FindResource("Toggle"), Margin = new Thickness(0, 0, 0, 8), ToolTip = "Ctrl+Alt+M" };
            st.Mute.Click += (s, e) => SetMicMute(st.Mute.IsChecked == true);
            body.Children.Add(st.Mute);
            var test = new Button { Content = "Test mic", Style = (Style)FindResource("Btn"), Margin = new Thickness(0, 0, 0, 8), ToolTip = "Records 5 seconds, then plays it back - exactly what Discord and games hear" };
            test.Click += (s, e) => TestMic(test);
            body.Children.Add(test);
            var ready = new Button { Content = "Discord-ready mic", Style = (Style)FindResource("Accent"), Margin = new Thickness(0, 0, 0, 8),
                ToolTip = "One click: unmute, a good level that stays put, voice EQ and rumble cut (needs Equalizer APO for the EQ part)" };
            ready.Click += (s, e) => DiscordReady();
            body.Children.Add(ready);
            st.Output = new ComboBox { Style = (Style)FindResource("Combo"), Margin = new Thickness(0, 0, 0, 10), ToolTip = "Which mic Windows uses (default for games and calls)" };
            st.Output.DropDownOpened += (s, e) => FillDevices(st.Output, 1, null, false);
            st.Output.SelectionChanged += (s, e) => { if (loading) return; var d = st.Output.SelectedItem as AudioDevice; if (d != null) { CoreAudio.SetDefault(d.Id); LoadMic(); Status(d.Name + " is now your mic for games and calls."); } };
            body.Children.Add(st.Output);
            nvBtn = new Button { Style = (Style)FindResource("Btn"), Margin = new Thickness(0, 0, 0, 8), Visibility = Visibility.Collapsed };
            nvBtn.Click += (s, e) => NoiseRemoval();
            body.Children.Add(nvBtn);
            dp.Children.Add(body);
            return shell;
        }

        // ================================================================ data -> controls
        void FillDevices(ComboBox box, int flow, string selectedId, bool withDefault)
        {
            loading = true;
            try
            {
                var list = new List<AudioDevice>();
                if (withDefault) list.Add(new AudioDevice { Id = "", Name = "Default output" });
                list.AddRange(CoreAudio.Devices(flow));
                box.ItemsSource = list;
                string want = selectedId ?? (list.FirstOrDefault(d => d.IsDefault) ?? new AudioDevice()).Id;
                box.SelectedItem = list.FirstOrDefault(d => d.Id == (want ?? ""));
            }
            catch { }
            loading = false;
        }

        void LoadValues()
        {
            loading = true;
            foreach (var st in strips.Values)
            {
                var c = eng.Chan(st.Id);
                st.Fader.Value = c.Volume; st.Pct.Text = c.Volume + "%";
                st.Mute.IsChecked = c.Muted; st.Mute.Content = c.Muted ? "Muted" : "Mute";
            }
            balance.Value = eng.S.ChatMix; UpdateBalanceText();
            loading = false;
            foreach (var st in strips.Values) FillDevices(st.Output, 0, eng.Chan(st.Id).OutputId ?? "", true);
            LoadMaster(); LoadMic();
        }
        void LoadMaster()
        {
            loading = true;
            var id = CoreAudio.DefaultId(0);
            if (id != null) { var l = CoreAudio.GetLevel(id); if (l >= 0) master.Value = l * 100; var m = CoreAudio.GetMute(id); masterMute.IsChecked = m; masterMute.Content = m ? "Muted" : "Mute"; }
            loading = false;
            FillDevices(outputPick, 0, null, false);
        }
        void LoadMic()
        {
            loading = true;
            var id = CoreAudio.DefaultId(1);
            if (id != null)
            {
                var l = CoreAudio.GetLevel(id); if (l >= 0) { mic.Fader.Value = l * 100; mic.Pct.Text = (int)Math.Round(l * 100) + "%"; }
                var m = CoreAudio.GetMute(id); mic.Mute.IsChecked = m; mic.Mute.Content = m ? "Muted" : "Mute";
            }
            loading = false;
            FillDevices(mic.Output, 1, null, false);
            var d = mic.Output.SelectedItem as AudioDevice; mic.Sub.Text = d != null ? d.Name : "No mic found";
            if (trayMicMute != null) trayMicMute.Checked = mic.Mute.IsChecked == true;
        }

        void UpdateBalanceText()
        {
            int m = eng.S.ChatMix;
            balanceText.Text = m == 0 ? "Balanced" : m > 0 ? string.Format("Game at {0}%", 100 - m) : string.Format("Chat at {0}%", 100 + m);
        }

        void UpdateSubs()
        {
            foreach (var st in strips.Values)
            {
                var apps = eng.Apps.Where(a => a.Channel == st.Id).ToList();
                st.Sub.Text = (apps.Count == 0 ? "No apps playing" : apps.Count == 1 ? "1 app" : apps.Count + " apps")
                    + (eng.DuckChat < 0.97f && (st.Id == "game" || st.Id == "music" || st.Id == "media") ? "  -  turned down (chat)" : "")
                    + (eng.DuckMic < 0.97f && (st.Id == "music" || st.Id == "media") ? "  -  turned down (you)" : "");
            }
        }

        void RebuildApps()
        {
            foreach (var st in strips.Values)
            {
                st.AppList.Children.Clear();
                foreach (var a in eng.Apps.Where(x => x.Channel == st.Id))
                    st.AppList.Children.Add(AppRow(a, st));
            }
            UpdateSubs();
        }

        UIElement AppRow(MixApp a, Strip st)
        {
            var p = new StackPanel { Margin = new Thickness(0, 4, 0, 4), Background = Brushes.Transparent };
            var line = new DockPanel();
            var icon = new Image { Width = 16, Height = 16, Margin = new Thickness(0, 0, 6, 0), Source = AppIcon(a.ExePath), Cursor = Cursors.Hand,
                ToolTip = "Click to mute / unmute just " + a.Name };
            var muted = eng.AppMuted(a.Key);
            var name = new TextBlock { Text = a.Name + (eng.S.BgLevel.ContainsKey(a.Key) ? (eng.InBackground(a.Key) ? "  (in background)" : "") : ""), FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center,
                ToolTip = (a.ExePath ?? a.Name) + (a.AutoChannel ? "" : "\n(you put this here)") };
            Action look = () => { var m = eng.AppMuted(a.Key); icon.Opacity = m ? 0.3 : 1; name.Opacity = m ? 0.45 : 1; name.TextDecorations = m ? TextDecorations.Strikethrough : null; };
            icon.MouseLeftButtonUp += (o, e) => { eng.ToggleAppMute(a); look(); Status(a.Name + (eng.AppMuted(a.Key) ? " muted." : " unmuted.")); };
            DockPanel.SetDock(icon, Dock.Left); line.Children.Add(icon); line.Children.Add(name);
            p.Children.Add(line); look();
            var s = new Slider { Style = (Style)FindResource("HFader"), Minimum = 0, Maximum = 100, Foreground = B(Color.FromArgb(170, st.Color.R, st.Color.G, st.Color.B)), Height = 20,
                Value = eng.AppVolume(a.Key), ToolTip = "This app's level inside the channel" };
            s.ValueChanged += (o, e) => { eng.S.AppVolume[a.Key] = (int)Math.Round(s.Value); if (eng.S.AppVolume[a.Key] == 100) eng.S.AppVolume.Remove(a.Key); eng.Apply(); dirty = true; };
            p.Children.Add(s);
            var cm = new ContextMenu();
            var move = new MenuItem { Header = "Move to" };
            foreach (var id in Engine.ChannelIds)
            {
                var target = id;
                var mi = new MenuItem { Header = Engine.ChannelNames[id], IsChecked = id == a.Channel };
                mi.Click += (o, e) => { eng.MoveApp(a, target); RebuildApps(); Status(a.Name + " moved to " + Engine.ChannelNames[target] + " - remembered from now on."); };
                move.Items.Add(mi);
            }
            cm.Items.Add(move);
            var playOn = new MenuItem { Header = "Play on", IsEnabled = CoreAudio.CanRoutePerApp };
            playOn.Items.Add(new MenuItem { Header = "(loading)" });
            playOn.SubmenuOpened += (o, e) =>
            {
                if (e.OriginalSource != playOn) return;
                playOn.Items.Clear();
                string cur; bool own = eng.S.AppOutput.TryGetValue(a.Key, out cur);
                var same = new MenuItem { Header = "Same as " + Engine.ChannelNames[a.Channel], IsChecked = !own };
                same.Click += (o2, e2) => { eng.SetAppOutput(a, null); Status(a.Name + " follows its channel's output again."); };
                playOn.Items.Add(same); playOn.Items.Add(new Separator());
                foreach (var d in CoreAudio.Devices(0))
                {
                    var dev = d;
                    var mi = new MenuItem { Header = d.Name, IsChecked = own && cur == d.Id };
                    mi.Click += (o2, e2) => { eng.SetAppOutput(a, dev.Id); Status(a.Name + " now plays on " + dev.Name + ". Some apps switch after a restart."); };
                    playOn.Items.Add(mi);
                }
            };
            cm.Items.Add(playOn);
            var bg = new MenuItem { Header = "When I'm in another app" };
            int curBg; bool hasBg = eng.S.BgLevel.TryGetValue(a.Key, out curBg);
            foreach (var opt in new[] { Tuple.Create(-1, "Keep playing"), Tuple.Create(20, "Turn down to 20%"), Tuple.Create(0, "Mute") })
            {
                var o = opt;
                var mi = new MenuItem { Header = o.Item2, IsChecked = o.Item1 < 0 ? !hasBg : hasBg && curBg == o.Item1 };
                mi.Click += (o2, e2) =>
                {
                    if (o.Item1 < 0) eng.S.BgLevel.Remove(a.Key); else eng.S.BgLevel[a.Key] = o.Item1;
                    eng.S.Save(); eng.Apply(); RebuildApps();
                    Status(o.Item1 < 0 ? a.Name + " keeps playing when you switch away." : a.Name + (o.Item1 == 0 ? " goes silent" : " turns down to 20%") + " while you're in another app, and comes back when you return.");
                };
                bg.Items.Add(mi);
            }
            cm.Items.Add(bg);
            var reset = new MenuItem { Header = "Back to automatic" };
            reset.Click += (o, e) => { eng.ResetApp(a); RebuildApps(); };
            cm.Items.Add(reset);
            p.ContextMenu = cm;
            return p;
        }

        // Runs 20 times a second, also while hidden in the tray (ducking needs the live levels).
        bool wasDucking;
        string defOut, defMic;   // default device ids, refreshed every 2 s (looking them up is too slow for 20x a second)
        // Headset first: if the headset is on, it should be the default output; if it's off, the fallback.
        string lastAuto;   // the device we last switched to (so we only act when the situation changes)
        void CheckAutoSwitch()
        {
            if (!eng.S.AutoSwitch || string.IsNullOrEmpty(eng.S.PrimaryOut)) return;
            try
            {
                var outs = CoreAudio.Devices(0);
                var head = outs.FirstOrDefault(d => d.Id == eng.S.PrimaryOut);
                var fb = outs.FirstOrDefault(d => d.Id == eng.S.FallbackOut);
                var want = head ?? fb;
                if (want == null || want.Id == lastAuto) return;
                lastAuto = want.Id;
                if (want.IsDefault) return;
                CoreAudio.SetDefault(want.Id); LoadMaster();
                var msg = head != null ? "Headset on - sound is on " + want.Name : "Headset off - sound moved to " + want.Name;
                Status(msg); if (eng.S.Overlay) osd.Flash(msg, -1);
            }
            catch { }
        }
        void UpdateDefaults() { try { defOut = CoreAudio.DefaultId(0); defMic = CoreAudio.DefaultId(1); micIds = CoreAudio.Devices(1).Select(d => d.Id).ToList(); } catch { } }

        // ---- v3.5 quiet hours: caps the master volume and halves Windows sounds on a schedule
        void TickQuiet()
        {
            bool q = eng.S.QuietOn && Engine.InQuiet(DateTime.Now.Hour, eng.S.QuietFrom, eng.S.QuietTo);
            if (q != eng.QuietNow)
            {
                eng.QuietNow = q; eng.Apply();
                var msg = q ? "Quiet hours until " + eng.S.QuietTo.ToString("00") + ":00 - volume capped at " + eng.S.QuietMax + "%" : "Quiet hours are over";
                Status(msg + "."); if (eng.S.Overlay) osd.Flash(msg, -1);
            }
            if (!q || defOut == null) return;
            var l = CoreAudio.GetLevel(defOut);
            if (l > eng.S.QuietMax / 100f + 0.005f) { CoreAudio.SetLevel(defOut, eng.S.QuietMax / 100f); LoadMaster(); }
        }

        // ---- v3.3 "you're muted": while your mic is muted (and you're not using push-to-talk), listen on every mic -
        // the real one behind Sonar too - and say so when you're really talking (not a cough or a key click).
        List<string> micIds = new List<string>();
        readonly VoiceDetector mutedVoice = new VoiceDetector(0.08f);
        DateTime lastMutedWarn = DateTime.MinValue;
        void TickMutedWarn()
        {
            if (!eng.S.MutedWarn || (eng.S.MicMode ?? "off") != "off" || mic.Mute.IsChecked != true) return;
            float pk = 0; foreach (var id in micIds) pk = Math.Max(pk, CoreAudio.DevicePeak(id));
            var now = DateTime.UtcNow;
            if (mutedVoice.Update(pk, now) && (now - lastMutedWarn).TotalSeconds > 20)
            {
                lastMutedWarn = now;
                osd.Flash("You're muted  -  Ctrl+Alt+M to unmute", -1);
                Status("You were talking while your mic is muted. Ctrl+Alt+M (or the Mic strip) unmutes it.");
            }
        }
        IntPtr lastFront; int frontTick;
        void TickFront()
        {
            if (++frontTick % 5 != 0 || eng.S.BgLevel.Count == 0) return;   // 4 times a second, only when an app uses it
            var h = GetForegroundWindow(); if (h == lastFront) return;
            lastFront = h;
            uint pid; GetWindowThreadProcessId(h, out pid);
            var a = eng.AppForPid(pid); var key = a != null ? a.Key : eng.KeyForPid(pid);
            if (key == eng.FrontKey) return;
            bool before = eng.FrontKey != null && eng.S.BgLevel.ContainsKey(eng.FrontKey), after = key != null && eng.S.BgLevel.ContainsKey(key);
            eng.FrontKey = key;
            if (before || after) { eng.Apply(); UpdateSubs(); RebuildApps(); }
        }
        void TickMeters()
        {
            TickPtt();
            TickFront();
            TickMutedWarn();
            var peaks = new Dictionary<string, float>();
            foreach (var id in Engine.ChannelIds) peaks[id] = 0;
            foreach (var a in eng.Apps) { float pk = a.Peak(); if (pk > peaks[a.Channel]) peaks[a.Channel] = pk; }
            float micPk = (eng.S.DuckOnMic || IsVisible) ? CoreAudio.DevicePeak(defMic) : 0;
            bool lv = eng.UpdateLevel(peaks["media"]);
            bool gd = eng.UpdateGuard(peaks, DateTime.UtcNow);
            if (eng.UpdateDuck(peaks["chat"], micPk) | lv | gd) eng.Apply();
            if (eng.GuardNote != null) { Status(eng.GuardNote + " (loud-sound guard)."); eng.GuardNote = null; }
            if (eng.Ducking != wasDucking) { wasDucking = eng.Ducking; UpdateSubs(); }
            if (!IsVisible) return;
            foreach (var st in strips.Values) SetMeter(st, peaks[st.Id]);
            SetMeter(mic, micPk);
            var outPk = CoreAudio.DevicePeak(defOut);
            masterMeter.Width = Math.Max(0, 190 * Math.Sqrt(outPk));
        }

        static readonly Dictionary<string, ImageSource> icons = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        static ImageSource AppIcon(string exe)
        {
            string k = exe ?? "";
            ImageSource src;
            if (icons.TryGetValue(k, out src)) return src;
            try
            {
                var ico = exe != null && File.Exists(exe) ? System.Drawing.Icon.ExtractAssociatedIcon(exe) : System.Drawing.SystemIcons.Application;
                src = Imaging.CreateBitmapSourceFromHIcon(ico.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromWidthAndHeight(16, 16));
                src.Freeze();
            }
            catch { src = null; }
            icons[k] = src; return src;
        }
        static void SetMeter(Strip st, float peak)
        {
            double target = Math.Sqrt(Math.Max(0, Math.Min(1, peak)));
            st.MeterLevel = target > st.MeterLevel ? target : st.MeterLevel * 0.86;   // fast up, smooth fall
            st.Meter.Height = Math.Max(0, 210 * st.MeterLevel);
        }

        void UsePreset(Preset p)
        {
            eng.ApplyPreset(p); eng.NoteLevelsChanged(); LoadValues();
            Status(p.Name + " preset on.");
            ShowTrayTip(p.Name + " preset on.", false);
        }

        // ---- presets bar: click = use, right-click = save here / rename / delete, + = new from the current levels
        StackPanel presetBar; WF.ToolStripMenuItem trayPresets;
        void RebuildPresets()
        {
            if (presetBar != null)
            {
                presetBar.Children.Clear();
                presetBar.Children.Add(Label("Presets", 0, 8));
                foreach (var p in eng.S.Presets)
                {
                    var preset = p;
                    var b = new Button { Content = p.Name, Style = (Style)FindResource("Btn"), Margin = new Thickness(0, 0, 6, 0), MinWidth = 64,
                        ToolTip = "Click to use " + p.Name + (eng.S.PresetKeys && eng.S.Presets.IndexOf(p) < 9 ? " (Ctrl+Alt+" + (eng.S.Presets.IndexOf(p) + 1) + ")" : "") + ". Right-click to save, rename or delete." };
                    b.Click += (s, e) => UsePreset(preset);
                    var cm = new ContextMenu();
                    var save = new MenuItem { Header = "Save the current levels here" };
                    save.Click += (s, e) => { eng.SavePreset(preset); Status("Saved the current levels as " + preset.Name + "."); };
                    var ren = new MenuItem { Header = "Rename..." };
                    ren.Click += (s, e) =>
                    {
                        var n = Ask("Rename preset", "New name for " + preset.Name + ":", preset.Name);
                        if (!string.IsNullOrWhiteSpace(n)) { preset.Name = n.Trim().Length > 18 ? n.Trim().Substring(0, 18) : n.Trim(); eng.S.Save(); RebuildPresets(); }
                    };
                    var del = new MenuItem { Header = "Delete", IsEnabled = eng.S.Presets.Count > 1 };
                    del.Click += (s, e) => { eng.S.Presets.Remove(preset); eng.S.Save(); RebuildPresets(); Status("Deleted the " + preset.Name + " preset."); };
                    cm.Items.Add(save); cm.Items.Add(ren); cm.Items.Add(new Separator()); cm.Items.Add(del);
                    b.ContextMenu = cm;
                    presetBar.Children.Add(b);
                }
                if (eng.S.Presets.Count < 8)
                {
                    var add = new Button { Content = "+", Style = (Style)FindResource("Btn"), MinWidth = 30, ToolTip = "New preset from the current levels" };
                    add.Click += (s, e) =>
                    {
                        var n = Ask("New preset", "Name for a preset with the current levels:", "Preset " + (eng.S.Presets.Count + 1));
                        if (string.IsNullOrWhiteSpace(n)) return;
                        var p = new Preset { Name = n.Trim().Length > 18 ? n.Trim().Substring(0, 18) : n.Trim() };
                        eng.S.Presets.Add(p); eng.SavePreset(p); RebuildPresets(); Status("Saved the current levels as " + p.Name + ".");
                    };
                    presetBar.Children.Add(add);
                }
            }
            if (trayPresets != null)
            {
                trayPresets.DropDownItems.Clear();
                foreach (var p in eng.S.Presets) { var preset = p; var it = new WF.ToolStripMenuItem(p.Name); it.Click += (s, e) => UsePreset(preset); trayPresets.DropDownItems.Add(it); }
            }
        }

        // Small dark text prompt (used for preset names).
        string Ask(string title, string question, string value)
        {
            var w = new Window { Title = title, Owner = this, Width = 360, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = (Brush)FindResource("Bg"), Foreground = (Brush)FindResource("Text"), FontFamily = FontFamily };
            var sp = new StackPanel { Margin = new Thickness(18) };
            sp.Children.Add(new TextBlock { Text = question, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap });
            var box = new TextBox { Text = value, Background = (Brush)FindResource("PanelHi"), Foreground = (Brush)FindResource("Text"), BorderBrush = (Brush)FindResource("Line"), Padding = new Thickness(6, 4, 6, 4), CaretBrush = Brushes.White };
            sp.Children.Add(box);
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var ok = new Button { Content = "OK", Style = (Style)FindResource("Accent"), IsDefault = true, MinWidth = 70, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Cancel", Style = (Style)FindResource("Btn"), IsCancel = true, MinWidth = 70 };
            string result = null;
            ok.Click += (s, e) => { result = box.Text; w.DialogResult = true; };
            row.Children.Add(ok); row.Children.Add(cancel); sp.Children.Add(row);
            w.Content = sp;
            w.Loaded += (s, e) => { box.Focus(); box.SelectAll(); };
            w.ShowDialog();
            return result;
        }

        // ---- updates
        Button updateBtn; GhRelease pending;
        void CheckForUpdate(bool manual)
        {
            if (manual) Status("Checking GitHub for a newer Hallow Mixer...");
            var th = new Thread(() =>
            {
                var r = Updater.Latest();
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    eng.S.LastUpdateCheck = DateTime.Now.ToString("o"); dirty = true;
                    if (r == null) { if (manual) Status("Couldn't reach GitHub - try again later."); return; }
                    if (!Updater.IsNewer(r)) { if (manual) Status("You have the newest version (v" + Program.Version + ")."); return; }
                    pending = r; updateBtn.Content = "Update to v" + r.Version.ToString(2); updateBtn.Visibility = Visibility.Visible;
                    updateBtn.ToolTip = (r.Notes ?? "").Length > 600 ? r.Notes.Substring(0, 600) + "..." : r.Notes;
                    Status("Hallow Mixer v" + r.Version.ToString(2) + " is out - click Update at the top (your settings are kept).");
                    if (!IsVisible) ShowTrayTip("Hallow Mixer v" + r.Version.ToString(2) + " is out - open it to update.", false);
                }));
            }) { IsBackground = true };
            th.Start();
        }
        void InstallUpdate()
        {
            if (pending == null) return;
            if (MessageBox.Show(this, "Install Hallow Mixer v" + pending.Version.ToString(2) + " now?\n\nIt downloads from GitHub, checks the file, and reopens by itself. Your levels and settings are kept.", "Update Hallow Mixer", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            updateBtn.IsEnabled = false; updateBtn.Content = "Updating...";
            var r = pending;
            var th = new Thread(() =>
            {
                eng.S.Save();
                var err = Updater.Install(r);
                Dispatcher.BeginInvoke(new Action(() => { if (err != null) { Status(err); updateBtn.IsEnabled = true; updateBtn.Content = "Update to v" + r.Version.ToString(2); } else Status("Installing the update - Hallow Mixer reopens in a few seconds."); }));
            }) { IsBackground = true };
            th.Start();
        }

        // ---- noise removal through NVIDIA Broadcast (free, needs an RTX card)
        Button nvBtn; bool? hasRtx;
        AudioDevice BroadcastMic() { return CoreAudio.Devices(1).FirstOrDefault(d => d.Name.IndexOf("NVIDIA Broadcast", StringComparison.OrdinalIgnoreCase) >= 0); }
        void UpdateNoiseButton()
        {
            if (hasRtx == null)
            {
                hasRtx = false;
                try
                {
                    using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                        if (k != null) foreach (var n in k.GetSubKeyNames()) { try { using (var sk = k.OpenSubKey(n)) { var d = sk == null ? null : sk.GetValue("DriverDesc") as string; if (d != null && d.IndexOf("RTX", StringComparison.OrdinalIgnoreCase) >= 0) hasRtx = true; } } catch { } }
                }
                catch { }
            }
            var bm = BroadcastMic();
            if (bm != null && bm.IsDefault) { nvBtn.Visibility = Visibility.Collapsed; return; }
            if (bm == null && hasRtx != true) { nvBtn.Visibility = Visibility.Collapsed; return; }
            nvBtn.Visibility = Visibility.Visible;
            nvBtn.Content = bm != null ? "Use NVIDIA Broadcast mic" : "Get noise removal";
            nvBtn.ToolTip = bm != null ? "Switch to NVIDIA Broadcast's noise-removed mic (for games and calls)" : "Your RTX card can remove background noise with NVIDIA Broadcast (free, from NVIDIA)";
        }
        void NoiseRemoval()
        {
            var bm = BroadcastMic();
            if (bm != null)
            {
                CoreAudio.SetDefault(bm.Id); LoadMic(); UpdateNoiseButton();
                Status("NVIDIA Broadcast mic is now your mic. Pick your real mic and turn on Noise Removal in the NVIDIA Broadcast app.");
                return;
            }
            try { Process.Start(new ProcessStartInfo("https://www.nvidia.com/en-us/geforce/broadcasting/broadcast-app/") { UseShellExecute = true }); } catch { }
            Status("Opened NVIDIA Broadcast's page. After installing, come back and click Use NVIDIA Broadcast mic.");
        }

        // ---- v2.5 now playing
        NowBar nowBar, miniNow; string shownGame;
        void ShowNow(MediaInfo m)
        {
            if (!eng.S.NowPlayingOn) m = null;
            var ch = NowChannel(m);
            var color = ch != null ? Colors[ch] : C("#8F88A0");
            var name = ch != null ? Engine.ChannelNames[ch] : null;
            if (nowBar != null) nowBar.Show(m, color, name);
            if (miniNow != null) miniNow.Show(m, color, name);
        }
        // the mixer channel of the app that's playing (null = not found)
        string NowChannel(MediaInfo m)
        {
            if (m == null || string.IsNullOrEmpty(m.Key)) return null;
            var app = eng.Apps.FirstOrDefault(a => a.Key == m.Key || (m.Key.Length > 3 && a.Key.StartsWith(m.Key)) || (a.Key.Length > 3 && m.Key.StartsWith(a.Key)));
            return app != null ? app.Channel : null;
        }
        void MediaCommand(string what)
        {
            NowPlaying.Command(what, () => NowPlaying.Poll(m => Dispatcher.BeginInvoke(new Action(() => ShowNow(m)))));
        }

        // ---- v2.3 clean chat voices
        ToggleButton cleanBtn; int cleanTick;
        void UpdateCleanButton()
        {
            if (cleanBtn == null) return;
            var bs = Engine.BroadcastSpeaker(CoreAudio.Devices(0));
            if (bs == null && hasRtx != true && !eng.S.ChatClean) { cleanBtn.Visibility = Visibility.Collapsed; return; }
            cleanBtn.Visibility = Visibility.Visible;
            cleanBtn.IsChecked = eng.S.ChatClean;
            cleanBtn.Content = eng.S.ChatClean ? "Clean voices: on" : bs != null ? "Clean voices" : "Get clean voices";
            cleanBtn.ToolTip = bs != null || eng.S.ChatClean
                ? "Sends Chat through NVIDIA Broadcast: background noise and echo removed from your friends' voices"
                : "Your RTX card can clean up your friends' voices with NVIDIA Broadcast (free, from NVIDIA)";
        }
        void CleanChat()
        {
            if (eng.S.ChatClean)
            {
                eng.SetChatClean(false, null); LoadValues(); UpdateCleanButton();
                Status("Clean voices off - Chat plays on its normal output again.");
                return;
            }
            var bs = Engine.BroadcastSpeaker(CoreAudio.Devices(0));
            if (bs == null)
            {
                cleanBtn.IsChecked = false;
                try { Process.Start(new ProcessStartInfo("https://www.nvidia.com/en-us/geforce/broadcasting/broadcast-app/") { UseShellExecute = true }); } catch { }
                Status("Opened NVIDIA Broadcast's page. After installing, come back and click Clean voices.");
                return;
            }
            eng.SetChatClean(true, bs.Id); LoadValues(); UpdateCleanButton();
            Status("Chat now goes through NVIDIA Broadcast. In the NVIDIA Broadcast app, under Speakers, pick your headset and turn on Noise Removal. Some apps switch after a restart.");
        }

        // Record 5 s from the default mic on a background thread, then play it back.
        void TestMic(Button btn)
        {
            var id = CoreAudio.DefaultId(1); if (id == null) { Status("No mic found."); return; }
            if (mic.Mute.IsChecked == true) { Status("Your mic is muted - unmute it first."); return; }
            btn.IsEnabled = false; btn.Content = "Talk now... 5"; Status("Recording 5 seconds - say something at your normal volume.");
            var countdown = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; int left = 5;
            countdown.Tick += (o, e) => { left--; if (left > 0) btn.Content = "Talk now... " + left; else countdown.Stop(); };
            countdown.Start();
            var t = new Thread(() =>
            {
                byte[] wav = null; float peak = 0; string err = null;
                try { wav = MicTest.Record(id, 5, out peak); } catch (Exception ex) { err = ex.Message; }
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    countdown.Stop();
                    if (err != null) { btn.Content = "Test mic"; btn.IsEnabled = true; Status("Mic test failed: " + err); return; }
                    btn.Content = "Playing back...";
                    string verdict = peak < 0.05f ? "very quiet - raise your mic level" : peak < 0.2f ? "a bit quiet" : peak > 0.97f ? "clipping (too loud) - lower your mic level" : "a good level";
                    Status("Playing back what others hear. Loudest point: " + (int)(peak * 100) + "% - " + verdict + ".");
                    var player = new System.Media.SoundPlayer(new MemoryStream(wav));
                    var pt = new Thread(() => { try { player.PlaySync(); } catch { } Dispatcher.BeginInvoke(new Action(() => { btn.Content = "Test mic"; btn.IsEnabled = true; })); }) { IsBackground = true };
                    pt.Start();
                }));
            }) { IsBackground = true };
            t.SetApartmentState(ApartmentState.MTA);
            t.Start();
        }

        // ---- v2.2 first-run setup
        SetupWindow setupWin; GamesWindow gamesWin;
        void OpenSetup()
        {
            if (setupWin != null && setupWin.IsLoaded) { setupWin.Activate(); return; }
            if (!IsVisible) ShowFromTray();
            setupWin = new SetupWindow(eng, (mode, ready, start) =>
            {
                lastAuto = null; UpdateDefaults(); eng.Refresh(); RebuildApps(); LoadValues();
                if (start) { SetStartWithWindows(true); if (trayStartup != null) trayStartup.Checked = true; }
                if (ready) DiscordReady();
                UpdateCleanButton();
                // rebuild the settings flyout so its switches and device boxes show what setup chose
                RebuildSettings();
                Status(mode == "sonar" ? "Set up to work with Sonar." : mode == "replace" ? "Set up to replace Sonar. Turn Sonar off in SteelSeries GG when you're ready." : "Set up as a mixer.");
            }) { Owner = this };
            setupWin.Closed += (s, e) => eng.S.Save();
            setupWin.Show();
        }

        // ---- v2.1 local API for Hallow Toolkit / phone dashboard
        MixerApi api;
        void StartApi()
        {
            if (api != null) return;
            api = new MixerApi(ApiState, ApiDo, Dispatcher) { ShareJson = ShareJson, ApplyShare = ApplyShare };
            if (!api.Start()) { api = null; return; }
            Dispatcher.ShutdownStarted += (s, e) => StopApi();
        }
        // ---- v2.4 Share your setup (through Hallow Toolkit)
        AudioDevice DefaultDevice(int flow) { var id = CoreAudio.DefaultId(flow); return id == null ? null : CoreAudio.Devices(flow).FirstOrDefault(d => d.Id == id); }
        AudioDevice ShareOutput()
        {
            // the headset if headset-first is set up, else Windows' default output
            if (!string.IsNullOrEmpty(eng.S.PrimaryOut)) { var p = CoreAudio.Devices(0).FirstOrDefault(d => d.Id == eng.S.PrimaryOut); if (p != null) return p; }
            var def = DefaultDevice(0); if (def != null && !Setup.IsSonar(def)) return def;
            return Setup.GuessHeadset(CoreAudio.Devices(0));   // Sonar's virtual devices don't count - EQ belongs on the real headset
        }
        AudioDevice ShareMic() { var d = DefaultDevice(1); return d != null && !Setup.IsSonar(d) ? d : Setup.GuessMic(CoreAudio.Devices(1)); }
        string ShareJson()
        {
            var o = ShareOutput(); var m = ShareMic();
            return Share.ToJson(Share.Make(eng.S, o == null ? null : o.Id, m == null ? null : m.Id));
        }
        string ApplyShare(string json)
        {
            SharedMix m;
            try { m = Share.FromJson(json); } catch { return "!That isn't Hallow Mixer settings."; }
            if (m == null) return "!That isn't Hallow Mixer settings.";
            var done = Share.Apply(eng, m, ShareOutput(), ShareMic());
            RebuildPresets(); RebuildApps(); LoadValues(); RebuildSettings();
            if (eqWin != null && eqWin.IsLoaded) eqWin.Reload();
            var text = "Imported a friend's setup: " + string.Join(", ", done);
            Status(text); return text;
        }
        void RebuildSettings()
        {
            var parent = settingsPanel.Parent as Grid; var vis = settingsPanel.Visibility;
            if (parent != null) { parent.Children.Remove(settingsPanel); settingsPanel = BuildSettings(); settingsPanel.Visibility = vis; parent.Children.Add(settingsPanel); }
        }
        void StopApi() { if (api != null) { api.Stop(); api = null; } }
        string ApiState()
        {
            var mid = CoreAudio.DefaultId(1);
            var chans = Engine.ChannelIds.Select(id => { var c = eng.Chan(id); return (object)new Dictionary<string, object> { { "id", id }, { "name", Engine.ChannelNames[id] }, { "volume", c.Volume }, { "muted", c.Muted } }; }).ToArray();
            var micInfo = new Dictionary<string, object> { { "level", mid == null ? -1 : (int)Math.Round(CoreAudio.GetLevel(mid) * 100) }, { "muted", mid != null && CoreAudio.GetMute(mid) } };
            return new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new Dictionary<string, object> {
                { "version", Program.Version }, { "channels", chans }, { "mic", micInfo }, { "chatMix", eng.S.ChatMix },
                { "presets", eng.S.Presets.Select(p => p.Name).ToArray() }, { "game", eng.CurrentGameName }, { "obs", obs.Connected },
                { "nowPlaying", NowState() } });
        }
        object NowState()
        {
            var m = eng.S.NowPlayingOn ? NowPlaying.Current : null; if (m == null) return null;
            return new Dictionary<string, object> { { "app", m.App }, { "title", m.Title }, { "artist", m.Artist }, { "playing", m.Playing }, { "canPrev", m.CanPrev }, { "canNext", m.CanNext }, { "channel", NowChannel(m) } };
        }
        // Commands: /set?ch=game&v=60 (or &d=-10), /mute?ch=game&m=toggle|1|0, /mic?mute=toggle|1|0, /preset?name=Chill
        string ApiDo(string path, Dictionary<string, string> q)
        {
            string ch; q.TryGetValue("ch", out ch);
            if ((path == "/set" || path == "/mute") && (ch == null || !Engine.ChannelIds.Contains(ch))) return "unknown channel";
            switch (path)
            {
                case "/set":
                {
                    var c = eng.Chan(ch); int v; string a;
                    if (q.TryGetValue("v", out a) && int.TryParse(a, out v)) c.Volume = Math.Max(0, Math.Min(100, v));
                    else if (q.TryGetValue("d", out a) && int.TryParse(a, out v)) c.Volume = Math.Max(0, Math.Min(100, c.Volume + v));
                    else return "missing v or d";
                    eng.NoteLevelsChanged(); eng.Apply(); LoadValues(); dirty = true;
                    return Engine.ChannelNames[ch] + " " + c.Volume + "%";
                }
                case "/mute":
                {
                    var c = eng.Chan(ch); string m; q.TryGetValue("m", out m);
                    c.Muted = m == "1" ? true : m == "0" ? false : !c.Muted;
                    eng.Apply(); LoadValues(); dirty = true;
                    return Engine.ChannelNames[ch] + (c.Muted ? " muted" : " on");
                }
                case "/mic":
                {
                    string m; q.TryGetValue("mute", out m);
                    var mid = CoreAudio.DefaultId(1); if (mid == null) return "no mic";
                    SetMicMute(m == "1" ? true : m == "0" ? false : !CoreAudio.GetMute(mid));
                    return CoreAudio.GetMute(mid) ? "Mic muted" : "Mic on";
                }
                case "/profile":
                {
                    string n; q.TryGetValue("name", out n);
                    var match = Profiles.List(eng.S.ProfileName).FirstOrDefault(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase));
                    if (match == null) return "no profile called " + n;
                    if (string.Equals(match, eng.S.ProfileName, StringComparison.OrdinalIgnoreCase)) return match + " is already on";
                    Dispatcher.BeginInvoke(new Action(() => UseProfile(match, false)));   // after this answer is sent
                    return "Switching to " + match;
                }
                case "/media":
                {
                    string d; q.TryGetValue("do", out d);
                    if (d != "playpause" && d != "next" && d != "prev") return "do must be playpause, next or prev";
                    MediaCommand(d); return d == "next" ? "Next" : d == "prev" ? "Previous" : "Play / pause";
                }
                case "/preset":
                {
                    string n; q.TryGetValue("name", out n);
                    var p = eng.S.Presets.FirstOrDefault(x => x.Name == n); if (p == null) return "no preset called " + n;
                    UsePreset(p); return p.Name + " preset on";
                }
            }
            return null;
        }

        // ---- v2.0 OBS link
        readonly ObsLink obs = new ObsLink(); StreamWindow streamWin; bool obsConnecting, obsHooked; DateTime lastObsTry = DateTime.MinValue;
        async System.Threading.Tasks.Task ConnectObs(bool quiet)
        {
            if (obsConnecting) return;
            obsConnecting = true; lastObsTry = DateTime.Now;
            if (!obsHooked)
            {
                obsHooked = true;
                obs.SceneChanged += scene => Dispatcher.BeginInvoke(new Action(() => OnObsScene(scene)));
                obs.Disconnected += why => Dispatcher.BeginInvoke(new Action(() =>
                {
                    Status(why); lastObsTry = DateTime.Now;
                    if (streamWin != null && streamWin.IsLoaded) { var r = streamWin.Refresh(); streamWin.Status(why); }
                }));
            }
            try
            {
                if (await obs.Connect(eng.S.ObsHost, eng.S.ObsPort, ObsLink.Unprotect(eng.S.ObsPass)))
                {
                    int n = await StreamMix.Apply(obs, eng.S, null);
                    Status("Connected to OBS" + (n > 0 ? " - stream mix set on " + n + " source(s)." : "."));
                    if (quiet && streamWin != null && streamWin.IsLoaded) { var r = streamWin.Refresh(); }
                }
                else if (!quiet) Status(obs.LastError);
            }
            finally { obsConnecting = false; }
        }
        void OnObsScene(string scene)
        {
            string pn; if (!eng.S.ScenePresets.TryGetValue(scene, out pn)) return;
            var p = eng.S.Presets.FirstOrDefault(x => x.Name == pn); if (p == null) return;
            UsePreset(p);
            Status("OBS scene " + scene + " - " + p.Name + " preset on.");
            if (eng.S.Overlay) osd.Flash(scene + ": " + p.Name, -1);
        }

        // ---- v1.7 mic pro
        CheckBox micKeepSwitch; string keptMic; DateTime lastKeepNote = DateTime.MinValue;
        // Every 2 s: if something else moved the mic level (Discord's automatic gain, a game, Windows), put it back.
        void KeepMicLevel()
        {
            if (!eng.S.MicKeep || string.IsNullOrEmpty(defMic)) return;
            try
            {
                var l = CoreAudio.GetLevel(defMic); if (l < 0) return;
                int now = (int)Math.Round(l * 100);
                if (keptMic != defMic || eng.S.MicKeepLevel < 0) { keptMic = defMic; eng.S.MicKeepLevel = now; dirty = true; return; }   // new mic: hold its level
                if (Math.Abs(now - eng.S.MicKeepLevel) < 2) return;
                CoreAudio.SetLevel(defMic, eng.S.MicKeepLevel / 100f);
                if (IsVisible) { loading = true; mic.Fader.Value = eng.S.MicKeepLevel; mic.Pct.Text = eng.S.MicKeepLevel + "%"; loading = false; }
                if ((DateTime.Now - lastKeepNote).TotalMinutes > 10)
                {
                    lastKeepNote = DateTime.Now;
                    Status("Something changed your mic level to " + now + "% - put it back to " + eng.S.MicKeepLevel + "%. If it keeps happening, turn off Automatic Gain Control in Discord > Voice & Video.");
                }
            }
            catch { }
        }

        // One click: a mic that sounds good in Discord and stays that way.
        void DiscordReady()
        {
            var id = CoreAudio.DefaultId(1); if (id == null) { Status("No mic found."); return; }
            var dev = CoreAudio.Devices(1).FirstOrDefault(d => d.Id == id);
            var name = dev != null ? dev.Name : "your mic";
            var done = new List<string>();
            if (CoreAudio.GetMute(id)) { SetMicMute(false); done.Add("unmuted"); }
            var lvl = CoreAudio.GetLevel(id);
            if (lvl >= 0 && lvl < 0.4f) { CoreAudio.SetLevel(id, 0.75f); lvl = 0.75f; done.Add("level raised to 75%"); }
            eng.S.MicKeep = true; eng.S.MicKeepLevel = lvl < 0 ? -1 : (int)Math.Round(lvl * 100); keptMic = id;
            if (micKeepSwitch != null) micKeepSwitch.IsChecked = true;
            done.Add("level held at " + eng.S.MicKeepLevel + "%");
            Eq.MakeDiscordReady(eng.S.Eq, id, name);
            done.Add(Eq.Connected ? "voice EQ + rumble cut on" : "voice EQ + rumble cut saved (starts once Equalizer APO is connected)");
            eng.S.Save(); LoadMic();
            if (eqWin != null && eqWin.IsLoaded) eqWin.Reload();
            var tip = BroadcastMic() == null && hasRtx == true ? " Noise removal: see below." : "";
            Status("Mic is Discord-ready: " + string.Join(", ", done) + ". In Discord, turn off Automatic Gain Control." + tip);
            if (eng.S.Overlay) osd.Flash("Mic is Discord-ready", -1);
        }

        void SetMicMute(bool m)
        {
            var id = CoreAudio.DefaultId(1); if (id == null) return;
            CoreAudio.SetMute(id, m); mic.Mute.IsChecked = m; mic.Mute.Content = m ? "Muted" : "Mute";
            if (trayMicMute != null) trayMicMute.Checked = m;
        }
        void Status(string t) { if (status != null) status.Text = t; }

        // ================================================================ hotkeys, tray, start with Windows
        void OnHandle()
        {
            var h = new WindowInteropHelper(this).Handle;
            int dark = 1; DwmSetWindowAttribute(h, 20, ref dark, 4);
            HwndSource.FromHwnd(h).AddHook(WndProc);
            var bad = new List<string>();
            if (!RegisterHotKey(h, 1, 0x4003, 0x21)) bad.Add("Ctrl+Alt+PageUp");   // MOD_ALT|MOD_CONTROL|MOD_NOREPEAT
            if (!RegisterHotKey(h, 2, 0x4003, 0x22)) bad.Add("Ctrl+Alt+PageDown");
            if (!RegisterHotKey(h, 3, 0x4003, 0x4D)) bad.Add("Ctrl+Alt+M");
            if (!RegisterHotKey(h, 4, 0x4003, 0x26)) bad.Add("Ctrl+Alt+Up");
            if (!RegisterHotKey(h, 5, 0x4003, 0x28)) bad.Add("Ctrl+Alt+Down");
            if (bad.Count > 0) Status("Another app already uses " + string.Join(", ", bad) + " - those hotkeys are off.");
            if (eng.S.PresetKeys) RegisterPresetKeys(true);
        }

        // ---- v2.8 preset hotkeys: Ctrl+Alt+1..9 = preset 1..9, Ctrl+Alt+0 = next preset (ids 10..19)
        [DllImport("user32.dll")] static extern int ToUnicodeEx(uint vk, uint scan, byte[] state, [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder buf, int size, uint flags, IntPtr layout);
        [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint thread);
        // On keyboards with AltGr (German, French, Polish...), Ctrl+Alt+digit types a character like { or @ - never take those.
        public static bool TypesSomething(uint vk)
        {
            var st = new byte[256]; st[0x11] = 0x80; st[0x12] = 0x80;   // Ctrl + Alt held
            var sb = new System.Text.StringBuilder(8);
            int n = ToUnicodeEx(vk, 0, st, sb, sb.Capacity, 4, GetKeyboardLayout(0));   // flag 4: don't change the keyboard state
            return n != 0;
        }
        public List<string> PresetKeysSkipped = new List<string>();
        void RegisterPresetKeys(bool on)
        {
            var h = new WindowInteropHelper(this).Handle; if (h == IntPtr.Zero) return;
            for (int i = 10; i <= 19; i++) UnregisterHotKey(h, i);
            PresetKeysSkipped.Clear();
            if (!on) return;
            for (int d = 0; d <= 9; d++)
            {
                uint vk = (uint)(0x30 + d); var name = "Ctrl+Alt+" + d;
                if (TypesSomething(vk)) { PresetKeysSkipped.Add(name + " (types a character on your keyboard)"); continue; }
                if (!RegisterHotKey(h, 10 + d, 0x4003, vk)) PresetKeysSkipped.Add(name + " (another app uses it)");
            }
            if (PresetKeysSkipped.Count > 0) Status("Preset hotkeys not used: " + string.Join(", ", PresetKeysSkipped) + ".");
        }
        int lastPreset = -1;
        void PresetHotkey(int d)
        {
            var list = eng.S.Presets; if (list.Count == 0) return;
            int i = d == 0 ? (lastPreset + 1) % list.Count : d - 1;
            if (i >= list.Count) { Notice("No preset " + d + " (you have " + list.Count + ")", -1); return; }
            lastPreset = i; var p = list[i];
            eng.ApplyPreset(p); eng.NoteLevelsChanged(); LoadValues();
            Status(p.Name + " preset on."); Notice(p.Name + " preset", -1);
        }
        IntPtr WndProc(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
        {
            if (msg != 0x0312) return IntPtr.Zero;
            int id = w.ToInt32();
            if (id == 1 || id == 2)
            {
                balance.Value = Math.Max(-100, Math.Min(100, balance.Value + (id == 1 ? 20 : -20)));
                Notice("Game / chat:  " + balanceText.Text, (balance.Value + 100) / 200.0);
            }
            else if (id == 3) { SetMicMute(!(mic.Mute.IsChecked == true)); Notice(mic.Mute.IsChecked == true ? "Mic muted" : "Mic on", -1); }
            else if (id == 4 || id == 5) FrontAppVolume(id == 4 ? 5 : -5);
            else if (id >= 10 && id <= 19) PresetHotkey(id - 10);
            handled = true; return IntPtr.Zero;
        }

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        static bool StartsWithWindows()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue("HallowMixer") != null;
        }
        static void SetStartWithWindows(bool on)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue("HallowMixer", "\"" + Assembly.GetExecutingAssembly().Location + "\" --tray");
                else k.DeleteValue("HallowMixer", false);
            }
        }

        // Starts a fresh copy (after this one has closed) without saving anything over settings.json.
        void Restart()
        {
            Settings.Frozen = true; exiting = true; StopApi(); tray.Visible = false;
            Process.Start(Assembly.GetExecutingAssembly().Location, "--after " + Process.GetCurrentProcess().Id + (IsVisible ? "" : " --tray"));
            Application.Current.Shutdown();
        }

        // ---- v3.6 profiles
        WF.ToolStripMenuItem trayProfiles;
        void UseProfile(string name, bool newCopy)
        {
            if (string.Equals(name, eng.S.ProfileName, StringComparison.OrdinalIgnoreCase) && !newCopy) return;
            try { Profiles.Switch(eng.S, name, newCopy); }
            catch (Exception ex) { Status("Couldn't switch profile: " + ex.Message); return; }
            if (newCopy) { Status("New profile \"" + name + "\" (a copy of your current setup) - change anything, it's saved to this profile."); RebuildProfiles(); return; }
            Restart();
        }
        void NewProfile()
        {
            var n = Profiles.Clean(Ask("New profile", "Name for the new profile (it starts as a copy of your current setup):", ""));
            if (n == null) return;
            if (Profiles.List(eng.S.ProfileName).Contains(n, StringComparer.OrdinalIgnoreCase)) { Status("There's already a profile called " + n + "."); return; }
            UseProfile(n, true);
        }
        void RebuildProfiles()
        {
            var names = Profiles.List(eng.S.ProfileName);
            if (trayProfiles != null)
            {
                trayProfiles.DropDownItems.Clear();
                foreach (var n in names)
                {
                    var name = n; var mi = new WF.ToolStripMenuItem(n) { Checked = string.Equals(n, eng.S.ProfileName, StringComparison.OrdinalIgnoreCase) };
                    mi.Click += (s, e) => UseProfile(name, false); trayProfiles.DropDownItems.Add(mi);
                }
                trayProfiles.DropDownItems.Add(new WF.ToolStripSeparator());
                var add = new WF.ToolStripMenuItem("New profile..."); add.Click += (s, e) => { ShowFromTray(); NewProfile(); }; trayProfiles.DropDownItems.Add(add);
            }
            if (profileBox != null)
            {
                loading = true; profileBox.ItemsSource = names; profileBox.SelectedItem = names.FirstOrDefault(x => string.Equals(x, eng.S.ProfileName, StringComparison.OrdinalIgnoreCase)); loading = false;
                if (profileDel != null) profileDel.IsEnabled = names.Count > 1;
            }
        }
        ComboBox profileBox; Button profileDel;

        void SetupTray()
        {
            tray = new WF.NotifyIcon { Text = "Hallow Mixer", Visible = true };
            try { tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch { tray.Icon = System.Drawing.SystemIcons.Application; }
            var menu = new WF.ContextMenuStrip();
            var open = new WF.ToolStripMenuItem("Open Hallow Mixer"); open.Font = new System.Drawing.Font(open.Font, System.Drawing.FontStyle.Bold);
            open.Click += (s, e) => ShowFromTray(); menu.Items.Add(open);
            trayPresets = new WF.ToolStripMenuItem("Presets");
            RebuildPresets();
            menu.Items.Add(trayPresets);
            trayProfiles = new WF.ToolStripMenuItem("Profile"); menu.Items.Add(trayProfiles);
            var miniItem = new WF.ToolStripMenuItem("Mini mixer (always on top)"); miniItem.Click += (s, e) => ToggleMini(); menu.Items.Add(miniItem);
            var center = new WF.ToolStripMenuItem("Balance game / chat"); center.Click += (s, e) => { balance.Value = 0; }; menu.Items.Add(center);
            trayMicMute = new WF.ToolStripMenuItem("Mute mic   Ctrl+Alt+M"); trayMicMute.Click += (s, e) => SetMicMute(!(mic.Mute.IsChecked == true)); menu.Items.Add(trayMicMute);
            menu.Items.Add(new WF.ToolStripSeparator());
            trayStartup = new WF.ToolStripMenuItem("Start with Windows") { Checked = StartsWithWindows() };
            trayStartup.Click += (s, e) => { SetStartWithWindows(!trayStartup.Checked); trayStartup.Checked = StartsWithWindows(); };
            menu.Items.Add(trayStartup);
            var exit = new WF.ToolStripMenuItem("Exit (apps go back to full volume)"); exit.Click += (s, e) => ExitApp(); menu.Items.Add(exit);
            tray.ContextMenuStrip = menu;
            RebuildProfiles();
            tray.MouseClick += (s, e) => { if (e.Button == WF.MouseButtons.Left) ShowFromTray(); };
        }
        readonly Osd osd = new Osd();
        MiniWindow mini;
        EqWindow eqWin;
        void ToggleMini()
        {
            if (mini == null)
            {
                var rows = Engine.ChannelIds.Select(id => Tuple.Create(id == "other" ? "System" : Engine.ChannelNames[id], strips[id].Color, strips[id].Fader)).ToList();
                rows.Add(Tuple.Create("Mic", mic.Color, mic.Fader));
                miniNow = new NowBar(true) { Margin = new Thickness(0, 10, 0, 0), Command = MediaCommand }; mini = new MiniWindow(eng.S, rows, balance, miniNow); ShowNow(NowPlaying.Current);
            }
            if (mini.IsVisible) { mini.Hide(); eng.S.MiniOpen = false; }
            else { mini.Show(); eng.S.MiniOpen = true; }
            eng.S.Save();
        }
        // hotkey feedback: on-screen pop-up (works over borderless games), or a tray balloon if pop-ups are off
        void Notice(string text, double bar)
        {
            if (IsVisible && IsActive) return;
            if (eng.S.Overlay) osd.Flash(text, bar); else ShowTrayTip(text, true);
        }
        void ShowTrayTip(string text, bool onlyIfHidden)
        {
            if (onlyIfHidden && IsVisible && IsActive) return;
            try { tray.ShowBalloonTip(1500, "Hallow Mixer", text, WF.ToolTipIcon.None); } catch { }
        }
        public void ShowFromTray()
        {
            Show(); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate(); Topmost = true; Topmost = false;
        }
        void HideToTray()
        {
            Hide();
            if (!eng.S.TrayTipShown) { eng.S.TrayTipShown = true; eng.S.Save(); ShowTrayTip("Still running by the clock - your levels stay on. Right-click the icon to exit.", false); }
        }
        void ExitApp()
        {
            exiting = true; StopApi();
            try { eng.S.Save(); eng.ResetAllApps(); } catch { }
            var h = new WindowInteropHelper(this).Handle; for (int i = 1; i <= 19; i++) UnregisterHotKey(h, i);
            tray.Visible = false; tray.Dispose();
            Application.Current.Shutdown();
        }
    }
}
