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
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace HallowMixer
{
    public static class Program
    {
        public const string Version = "1.0";
        [STAThread]
        public static void Main(string[] args)
        {
            bool created;
            var mutex = new Mutex(true, @"Local\HallowMixer", out created);
            var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\HallowMixerShow");
            if (!created) { showEvent.Set(); return; }   // already running: bring that one to the front
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

    public class MixerWindow : Window
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);

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
            SetupTray();
            eng.Refresh(); RebuildApps(); LoadValues();
            meterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) }; meterTimer.Tick += (s, e) => TickMeters(); meterTimer.Start();
            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; refreshTimer.Tick += (s, e) => { if (eng.Refresh()) RebuildApps(); else UpdateSubs(); }; refreshTimer.Start();
            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; saveTimer.Tick += (s, e) => { if (dirty) { dirty = false; eng.S.Save(); } }; saveTimer.Start();
            if (!startHidden) Show(); else { WindowState = WindowState.Minimized; Show(); Hide(); WindowState = WindowState.Normal; }
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

            // ---- header: name + master output
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 12), LastChildFill = false };
            var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            title.Children.Add(new TextBlock { Text = "HALLOW", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = B("#F07820") });
            title.Children.Add(new TextBlock { Text = "  MIXER", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = B("#A98BE0"), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 5) });
            DockPanel.SetDock(title, Dock.Left); head.Children.Add(title);
            var mp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
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
            balance.ValueChanged += (s, e) => { if (loading) return; eng.S.ChatMix = (int)Math.Round(balance.Value); eng.Apply(); dirty = true; UpdateBalanceText(); };
            balance.MouseDoubleClick += (s, e) => { balance.Value = 0; };
            bl.Children.Add(balance);
            bl.Children.Add(new TextBlock { Text = "CHAT", FontWeight = FontWeights.Bold, Foreground = B("#A98BE0"), VerticalAlignment = VerticalAlignment.Center });
            balanceText = new TextBlock { Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Width = 170 };
            bl.Children.Add(balanceText);
            DockPanel.SetDock(bl, Dock.Left); bg.Children.Add(bl);
            var pr = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            pr.Children.Add(Label("Presets", 0, 8));
            foreach (var p in eng.S.Presets)
            {
                var preset = p;
                var b = new Button { Content = p.Name, Style = (Style)FindResource("Btn"), Margin = new Thickness(0, 0, 6, 0), MinWidth = 70,
                    ToolTip = "Click to use " + p.Name + ". Right-click to save the current levels as " + p.Name + "." };
                b.Click += (s, e) => UsePreset(preset);
                b.MouseRightButtonUp += (s, e) => { eng.SavePreset(preset); Status("Saved the current levels as " + preset.Name + "."); };
                pr.Children.Add(b);
            }
            bg.Children.Add(pr);
            bar.Child = bg;
            Grid.SetRow(bar, 2); root.Children.Add(bar);

            // ---- status line
            status = new TextBlock { Foreground = (Brush)FindResource("Muted"), FontSize = 11.5, Margin = new Thickness(6, 10, 6, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetRow(status, 3); root.Children.Add(status);
            Status(CoreAudio.CanRoutePerApp ? "Right-click an app to move it to another channel.  Ctrl+Alt+PageUp/PageDown = game/chat balance,  Ctrl+Alt+M = mute mic."
                                            : "Per-channel output isn't supported on this Windows version - volumes still work.");
            return root;
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
            var body = new StackPanel(); DockPanel.SetDock(body, Dock.Top);
            body.Children.Add(FaderBlock(st, "Volume of every app in " + Engine.ChannelNames[id]));
            st.Fader.ValueChanged += (s, e) => { if (loading) return; eng.Chan(id).Volume = (int)Math.Round(st.Fader.Value); st.Pct.Text = (int)st.Fader.Value + "%"; eng.Apply(); dirty = true; };
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
            var body = new StackPanel(); DockPanel.SetDock(body, Dock.Top);
            body.Children.Add(FaderBlock(st, "Your mic level (what games and Discord get)"));
            st.Fader.ValueChanged += (s, e) => { if (loading) return; var mid = CoreAudio.DefaultId(1); if (mid != null) CoreAudio.SetLevel(mid, (float)(st.Fader.Value / 100)); st.Pct.Text = (int)st.Fader.Value + "%"; };
            st.Pct = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
            body.Children.Add(st.Pct);
            st.Mute = new ToggleButton { Content = "Mute", Style = (Style)FindResource("Toggle"), Margin = new Thickness(0, 0, 0, 8), ToolTip = "Ctrl+Alt+M" };
            st.Mute.Click += (s, e) => SetMicMute(st.Mute.IsChecked == true);
            body.Children.Add(st.Mute);
            st.Output = new ComboBox { Style = (Style)FindResource("Combo"), Margin = new Thickness(0, 0, 0, 10), ToolTip = "Which mic Windows uses (default for games and calls)" };
            st.Output.DropDownOpened += (s, e) => FillDevices(st.Output, 1, null, false);
            st.Output.SelectionChanged += (s, e) => { if (loading) return; var d = st.Output.SelectedItem as AudioDevice; if (d != null) { CoreAudio.SetDefault(d.Id); LoadMic(); Status(d.Name + " is now your mic for games and calls."); } };
            body.Children.Add(st.Output);
            dp.Children.Add(body);
            dp.Children.Add(new TextBlock { Text = "Using SteelSeries Sonar? Its mic stays the default - set your real mic's level in Sonar.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = (Brush)FindResource("Muted") });
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
                st.Sub.Text = apps.Count == 0 ? "No apps playing" : apps.Count == 1 ? "1 app" : apps.Count + " apps";
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
            var name = new TextBlock { Text = a.Name, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = (a.ExePath ?? a.Name) + (a.AutoChannel ? "" : "\n(you put this here)") };
            p.Children.Add(name);
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
            var reset = new MenuItem { Header = "Back to automatic" };
            reset.Click += (o, e) => { eng.ResetApp(a); RebuildApps(); };
            cm.Items.Add(reset);
            p.ContextMenu = cm;
            return p;
        }

        void TickMeters()
        {
            if (!IsVisible) return;
            foreach (var st in strips.Values)
            {
                float pk = 0; foreach (var a in eng.Apps) if (a.Channel == st.Id) pk = Math.Max(pk, a.Peak());
                SetMeter(st, pk);
            }
            SetMeter(mic, CoreAudio.DevicePeak(CoreAudio.DefaultId(1)));
            var outPk = CoreAudio.DevicePeak(CoreAudio.DefaultId(0));
            masterMeter.Width = Math.Max(0, 190 * Math.Sqrt(outPk));
        }
        static void SetMeter(Strip st, float peak)
        {
            double target = Math.Sqrt(Math.Max(0, Math.Min(1, peak)));
            st.MeterLevel = target > st.MeterLevel ? target : st.MeterLevel * 0.86;   // fast up, smooth fall
            st.Meter.Height = Math.Max(0, 210 * st.MeterLevel);
        }

        void UsePreset(Preset p)
        {
            eng.ApplyPreset(p); LoadValues();
            Status(p.Name + " preset on.");
            ShowTrayTip(p.Name + " preset on.", false);
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
            if (bad.Count > 0) Status("Another app already uses " + string.Join(", ", bad) + " - those hotkeys are off.");
        }
        IntPtr WndProc(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
        {
            if (msg != 0x0312) return IntPtr.Zero;
            int id = w.ToInt32();
            if (id == 1 || id == 2)
            {
                balance.Value = Math.Max(-100, Math.Min(100, balance.Value + (id == 1 ? 20 : -20)));
                ShowTrayTip(balanceText.Text, true);
            }
            else if (id == 3) { SetMicMute(!(mic.Mute.IsChecked == true)); ShowTrayTip(mic.Mute.IsChecked == true ? "Mic muted" : "Mic on", true); }
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

        void SetupTray()
        {
            tray = new WF.NotifyIcon { Text = "Hallow Mixer", Visible = true };
            try { tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch { tray.Icon = System.Drawing.SystemIcons.Application; }
            var menu = new WF.ContextMenuStrip();
            var open = new WF.ToolStripMenuItem("Open Hallow Mixer"); open.Font = new System.Drawing.Font(open.Font, System.Drawing.FontStyle.Bold);
            open.Click += (s, e) => ShowFromTray(); menu.Items.Add(open);
            var presets = new WF.ToolStripMenuItem("Presets");
            foreach (var p in eng.S.Presets) { var preset = p; var it = new WF.ToolStripMenuItem(p.Name); it.Click += (s, e) => UsePreset(preset); presets.DropDownItems.Add(it); }
            menu.Items.Add(presets);
            var center = new WF.ToolStripMenuItem("Balance game / chat"); center.Click += (s, e) => { balance.Value = 0; }; menu.Items.Add(center);
            trayMicMute = new WF.ToolStripMenuItem("Mute mic   Ctrl+Alt+M"); trayMicMute.Click += (s, e) => SetMicMute(!(mic.Mute.IsChecked == true)); menu.Items.Add(trayMicMute);
            menu.Items.Add(new WF.ToolStripSeparator());
            trayStartup = new WF.ToolStripMenuItem("Start with Windows") { Checked = StartsWithWindows() };
            trayStartup.Click += (s, e) => { SetStartWithWindows(!trayStartup.Checked); trayStartup.Checked = StartsWithWindows(); };
            menu.Items.Add(trayStartup);
            var exit = new WF.ToolStripMenuItem("Exit (apps go back to full volume)"); exit.Click += (s, e) => ExitApp(); menu.Items.Add(exit);
            tray.ContextMenuStrip = menu;
            tray.MouseClick += (s, e) => { if (e.Button == WF.MouseButtons.Left) ShowFromTray(); };
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
            exiting = true;
            try { eng.S.Save(); eng.ResetAllApps(); } catch { }
            var h = new WindowInteropHelper(this).Handle; for (int i = 1; i <= 3; i++) UnregisterHotKey(h, i);
            tray.Visible = false; tray.Dispose();
            Application.Current.Shutdown();
        }
    }
}
