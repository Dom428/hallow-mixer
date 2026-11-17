// Hallow Mixer - EQ for each device, through Equalizer APO (free, open source: equalizerapo.sourceforge.io).
// Hallow Mixer writes C:\ProgramData\HallowMixer\eq.txt; Equalizer APO's config.txt includes it once ("Connect"),
// and Equalizer APO reloads it by itself whenever it changes.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace HallowMixer
{
    [DataContract] public class EqState
    {
        [DataMember] public string Name;
        [DataMember] public bool Capture;
        [DataMember] public bool On;
        [DataMember] public string Preset = "Flat";
        [DataMember] public int[] Gains = new int[10];
        [DataMember] public int LowCut;        // v1.7: high-pass filter in Hz for mics (0 = off) - removes rumble, desk bumps, fan hum
    }

    public static class Eq
    {
        public static readonly int[] Bands = { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };
        public static readonly string[] BandNames = { "31", "62", "125", "250", "500", "1k", "2k", "4k", "8k", "16k" };
        public static readonly Dictionary<string, int[]> OutPresets = new Dictionary<string, int[]> {
            { "Flat", new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } },
            { "Bass boost", new[] { 6, 5, 4, 2, 0, 0, 0, 0, 0, 0 } },
            { "Footsteps (FPS)", new[] { -4, -3, -2, 0, 1, 2, 4, 5, 3, 0 } },
            { "Voice clarity", new[] { -6, -4, -2, 0, 1, 3, 4, 3, 1, -1 } },
            { "Movie", new[] { 4, 3, 2, 0, -1, 0, 1, 2, 3, 3 } },
            { "Music", new[] { 4, 3, 1, 0, -1, -1, 0, 2, 3, 4 } } };
        public static readonly Dictionary<string, int[]> MicPresets = new Dictionary<string, int[]> {
            { "Flat", new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } },
            { "Discord-ready", new[] { 0, -1, -2, -1, 0, 1, 3, 3, 1, -2 } },
            { "Clear voice", new[] { -12, -8, -3, 0, 1, 2, 3, 2, 0, -2 } },
            { "Warm", new[] { -6, 2, 3, 1, 0, -1, 0, 1, 0, -2 } },
            { "Bright", new[] { -8, -5, -1, 0, 0, 1, 3, 4, 4, 2 } },
            { "Cut rumble", new[] { -12, -8, -4, 0, 0, 0, 0, 0, 0, 0 } } };

        public static string ConfigFile { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"HallowMixer\eq.txt"); } }

        // Equalizer APO's own config folder (null = not installed)
        public static string ApoConfigDir
        {
            get
            {
                var p = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\EqualizerAPO", "ConfigPath", null) as string;
                if (string.IsNullOrEmpty(p))
                {
                    var inst = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\EqualizerAPO", "InstallPath", null) as string;
                    if (!string.IsNullOrEmpty(inst)) p = Path.Combine(inst, "config");
                }
                return !string.IsNullOrEmpty(p) && Directory.Exists(p) ? p : null;
            }
        }
        public static bool Installed { get { return ApoConfigDir != null; } }
        public static bool Connected
        {
            get
            {
                var d = ApoConfigDir; if (d == null) return false;
                try { return File.ReadAllText(Path.Combine(d, "config.txt")).IndexOf(ConfigFile, StringComparison.OrdinalIgnoreCase) >= 0; } catch { return false; }
            }
        }
        // Equalizer APO's Configurator (where you pick which devices it works on)
        public static string Configurator
        {
            get
            {
                var inst = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\EqualizerAPO", "InstallPath", null) as string;
                var f = inst == null ? null : Path.Combine(inst, "Configurator.exe");
                return f != null && File.Exists(f) ? f : null;
            }
        }

        // One admin step: back up Equalizer APO's config.txt and replace it with a single line that includes ours.
        // (Its default config has demo filters and a -6 dB preamp that would change your sound.)
        public static bool Connect()
        {
            var d = ApoConfigDir; if (d == null) return false;
            Write(new Dictionary<string, EqState>());   // make sure our file exists first
            var cfg = Path.Combine(d, "config.txt");
            var script = "$c='" + cfg.Replace("'", "''") + "'; if (Test-Path $c) { Copy-Item $c ($c + '.before-hallow-mixer.bak') -Force }; " +
                         "Set-Content -LiteralPath $c -Encoding UTF8 -Value @('# Managed by Hallow Mixer - your EQ is in Hallow Mixer > EQ. The old file is config.txt.before-hallow-mixer.bak', 'Include: " + ConfigFile.Replace("'", "''") + "')";
            try
            {
                var p = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"" + script.Replace("\"", "\\\"") + "\"")
                    { Verb = "runas", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
                p.WaitForExit(30000);
            }
            catch { return false; }   // you clicked No on the admin prompt
            return Connected;
        }

        public static readonly int[] LowCuts = { 0, 60, 80, 100, 120 };

        // One click: the Discord-ready mic sound on this mic (voice EQ + 80 Hz low cut).
        public static EqState MakeDiscordReady(Dictionary<string, EqState> eq, string micId, string micName)
        {
            EqState st;
            if (!eq.TryGetValue(micId, out st)) { st = new EqState(); eq[micId] = st; }
            st.Name = "Mic: " + micName; st.Capture = true; st.On = true; st.Preset = "Discord-ready";
            st.Gains = (int[])MicPresets["Discord-ready"].Clone(); st.LowCut = 80;
            Write(eq);
            return st;
        }

        static string Guid(string deviceId) { int i = deviceId.LastIndexOf('.'); return i >= 0 ? deviceId.Substring(i + 1) : deviceId; }

        // Writes every device's EQ. Equalizer APO picks the change up within a second.
        public static void Write(Dictionary<string, EqState> eq)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Written by Hallow Mixer - change it in Hallow Mixer > EQ (edits here are overwritten).");
            foreach (var kv in eq)
            {
                var e = kv.Value; if (!e.On || e.Gains == null || (e.Gains.All(g => g == 0) && e.LowCut <= 0)) continue;
                sb.AppendLine();
                sb.AppendLine("# " + (e.Name ?? kv.Key) + " - " + e.Preset);
                sb.AppendLine("Device: " + Guid(kv.Key));
                int max = Math.Max(0, e.Gains.Max());
                sb.AppendLine("Preamp: " + (-max).ToString(CultureInfo.InvariantCulture) + " dB");   // headroom, so boosts never distort
                if (e.Gains.Any(g => g != 0))
                    sb.AppendLine("GraphicEQ: " + string.Join("; ", Bands.Select((f, i) => f.ToString(CultureInfo.InvariantCulture) + " " + e.Gains[i].ToString(CultureInfo.InvariantCulture))));
                if (e.LowCut > 0) sb.AppendLine("Filter: ON HP Fc " + e.LowCut.ToString(CultureInfo.InvariantCulture) + " Hz");   // true 12 dB/oct high-pass
            }
            sb.AppendLine();
            sb.AppendLine("Device: all");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile));
                File.WriteAllText(ConfigFile, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }
    }

    // The EQ window: pick a device, pick a preset or drag the 10 bands.
    public class EqWindow : Window
    {
        readonly Settings s;
        ComboBox device, lowCut; DockPanel lowCutRow; CheckBox onSwitch; WrapPanel presetRow; TextBlock status, apoText; Button connectBtn, configBtn;
        readonly Slider[] bands = new Slider[10]; readonly TextBlock[] vals = new TextBlock[10];
        bool loading; readonly DispatcherTimer writeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };

        static SolidColorBrush B(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }

        public EqWindow(Settings settings)
        {
            s = settings;
            Title = "Hallow Mixer - EQ"; Width = 640; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.CanMinimize;
            Background = (Brush)FindResource("Bg"); Foreground = (Brush)FindResource("Text"); FontFamily = new FontFamily("Segoe UI"); FontSize = 12.5;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            writeTimer.Tick += (o, e) => { writeTimer.Stop(); Eq.Write(s.Eq); s.Save(); };

            var sp = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            var head = new DockPanel();
            head.Children.Add(new TextBlock { Text = "EQ", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = B("#F07820") });
            sp.Children.Add(head);
            apoText = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 4, 0, 8) };
            sp.Children.Add(apoText);
            var ab = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            connectBtn = new Button { Content = "Connect to Equalizer APO", Style = (Style)FindResource("Accent"), Margin = new Thickness(0, 0, 8, 0) };
            connectBtn.Click += (o, e) => { if (Eq.Connect()) { Eq.Write(s.Eq); Status("Connected - your EQ now works."); } else Status("Not connected (the admin prompt was cancelled, or Equalizer APO isn't installed)."); RefreshApo(); };
            configBtn = new Button { Content = "Pick devices (Equalizer APO)", Style = (Style)FindResource("Btn"), ToolTip = "Opens Equalizer APO's Configurator - tick the devices EQ should work on, then restart the PC." };
            configBtn.Click += (o, e) => { var c = Eq.Configurator; if (c != null) try { Process.Start(new ProcessStartInfo(c) { UseShellExecute = true, Verb = "runas" }); } catch { } };
            ab.Children.Add(connectBtn); ab.Children.Add(configBtn);
            sp.Children.Add(ab);

            var dr = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var dl = new TextBlock { Text = "Device", Foreground = (Brush)FindResource("Muted"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            DockPanel.SetDock(dl, Dock.Left); dr.Children.Add(dl);
            onSwitch = new CheckBox { Content = "EQ on", Style = (Style)FindResource("Switch"), Margin = new Thickness(14, 0, 0, 0) };
            onSwitch.Click += (o, e) => { var st = Cur(); if (st != null) { st.On = onSwitch.IsChecked == true; Changed(); } };
            DockPanel.SetDock(onSwitch, Dock.Right); dr.Children.Add(onSwitch);
            device = new ComboBox { Style = (Style)FindResource("Combo") };
            device.SelectionChanged += (o, e) => LoadDevice();
            dr.Children.Add(device);
            sp.Children.Add(dr);

            presetRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            sp.Children.Add(presetRow);
            lowCutRow = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            var lcl = new TextBlock { Text = "Low cut", Foreground = (Brush)FindResource("Muted"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            lowCut = new ComboBox { Style = (Style)FindResource("Combo"), Width = 120, ToolTip = "Removes rumble, desk bumps and fan hum below this pitch (voices stay untouched). 80 Hz suits most voices." };
            lowCut.ItemsSource = Eq.LowCuts.Select(h => h == 0 ? "Off" : h + " Hz").ToList();
            lowCut.SelectionChanged += (o, e) => { if (loading) return; var st = Cur(); if (st == null || lowCut.SelectedIndex < 0) return; st.LowCut = Eq.LowCuts[lowCut.SelectedIndex]; if (st.LowCut > 0) { st.On = true; onSwitch.IsChecked = true; } Changed(); };
            var lch = new TextBlock { Text = "removes rumble and desk bumps", Foreground = (Brush)FindResource("Muted"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            DockPanel.SetDock(lcl, Dock.Left); DockPanel.SetDock(lowCut, Dock.Left);
            lowCutRow.Children.Add(lcl); lowCutRow.Children.Add(lowCut); lowCutRow.Children.Add(lch);
            sp.Children.Add(lowCutRow);

            var grid = new Grid { Height = 210, Background = (Brush)FindResource("Panel") };
            for (int i = 0; i < 10; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                var col = new DockPanel { Margin = new Thickness(0, 8, 0, 6) };
                var name = new TextBlock { Text = Eq.BandNames[i], HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Brush)FindResource("Muted"), FontSize = 11 };
                vals[i] = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, FontSize = 11, FontWeight = FontWeights.SemiBold };
                DockPanel.SetDock(name, Dock.Bottom); DockPanel.SetDock(vals[i], Dock.Top);
                col.Children.Add(name); col.Children.Add(vals[i]);
                int band = i;
                bands[i] = new Slider { Style = (Style)FindResource("VFader"), Minimum = -12, Maximum = 12, SmallChange = 1, LargeChange = 3, Foreground = B("#F07820"),
                    HorizontalAlignment = HorizontalAlignment.Center, IsSnapToTickEnabled = true, TickFrequency = 1, ToolTip = Eq.BandNames[i] + " Hz (scroll or drag, double-click = 0)" };
                bands[i].ValueChanged += (o, e) =>
                {
                    vals[band].Text = (bands[band].Value > 0 ? "+" : "") + bands[band].Value.ToString("0");
                    if (loading) return;
                    var st = Cur(); if (st == null) return;
                    st.Gains[band] = (int)bands[band].Value; st.Preset = "Custom"; st.On = true; onSwitch.IsChecked = true; Changed();
                };
                bands[i].MouseDoubleClick += (o, e) => { bands[band].Value = 0; };
                bands[i].PreviewMouseWheel += (o, e) => { bands[band].Value = Math.Max(-12, Math.Min(12, bands[band].Value + (e.Delta > 0 ? 1 : -1))); e.Handled = true; };
                col.Children.Add(bands[i]);
                Grid.SetColumn(col, i); grid.Children.Add(col);
            }
            var shell = new Border { CornerRadius = new CornerRadius(8), Background = (Brush)FindResource("Panel"), Child = grid, Padding = new Thickness(6) };
            sp.Children.Add(shell);
            status = new TextBlock { Foreground = (Brush)FindResource("Muted"), FontSize = 11.5, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap };
            sp.Children.Add(status);
            Content = sp;

            FillDevices();
            RefreshApo();
            Activated += (o, e) => RefreshApo();
        }

        void Status(string t) { status.Text = t; }
        void RefreshApo()
        {
            bool inst = Eq.Installed, conn = inst && Eq.Connected;
            apoText.Text = !inst ? "EQ works through Equalizer APO (free, open source). It isn't installed yet - get it from equalizerapo.sourceforge.io, tick your headset and mic in its Configurator, restart, then come back here."
                : !conn ? "Equalizer APO is installed. Click Connect once (it asks for admin) so Hallow Mixer can control it - this also removes its demo settings."
                : "Equalizer APO is connected. Only devices ticked in its Configurator get EQ - use Pick devices to add more (needs a restart).";
            connectBtn.Visibility = inst && !conn ? Visibility.Visible : Visibility.Collapsed;
            configBtn.Visibility = inst ? Visibility.Visible : Visibility.Collapsed;
        }

        void FillDevices()
        {
            var items = new List<AudioDevice>();
            foreach (var d in CoreAudio.Devices(0)) items.Add(new AudioDevice { Id = d.Id, Name = d.Name, IsDefault = d.IsDefault });
            foreach (var d in CoreAudio.Devices(1)) items.Add(new AudioDevice { Id = d.Id, Name = "Mic: " + d.Name, IsDefault = false });
            device.ItemsSource = items;
            var def = items.FirstOrDefault(d => d.IsDefault) ?? items.FirstOrDefault();
            device.SelectedItem = def;
        }

        EqState Cur()
        {
            var d = device.SelectedItem as AudioDevice; if (d == null) return null;
            EqState st;
            if (!s.Eq.TryGetValue(d.Id, out st)) { st = new EqState { Name = d.Name, Capture = d.Name.StartsWith("Mic: ") }; s.Eq[d.Id] = st; }
            if (st.Gains == null || st.Gains.Length != 10) st.Gains = new int[10];
            st.Name = d.Name;
            return st;
        }

        void LoadDevice()
        {
            var st = Cur(); if (st == null) return;
            loading = true;
            onSwitch.IsChecked = st.On;
            for (int i = 0; i < 10; i++) { bands[i].Value = st.Gains[i]; vals[i].Text = (st.Gains[i] > 0 ? "+" : "") + st.Gains[i]; }
            lowCutRow.Visibility = st.Capture ? Visibility.Visible : Visibility.Collapsed;
            lowCut.SelectedIndex = Math.Max(0, Array.IndexOf(Eq.LowCuts, st.LowCut));
            loading = false;
            presetRow.Children.Clear();
            var presets = st.Capture ? Eq.MicPresets : Eq.OutPresets;
            foreach (var kv in presets)
            {
                var name = kv.Key; var gains = kv.Value;
                var b = new Button { Content = name, Style = (Style)FindResource(st.Preset == name ? "Accent" : "Btn"), Margin = new Thickness(0, 0, 6, 6) };
                b.Click += (o, e) =>
                {
                    var cur = Cur(); cur.Gains = (int[])gains.Clone(); cur.Preset = name;
                    if (cur.Capture && name == "Discord-ready") cur.LowCut = 80; else if (cur.Capture && name == "Flat") cur.LowCut = 0;
                    cur.On = name != "Flat" || cur.LowCut > 0; Changed(); LoadDevice();
                    Status(name + " on " + cur.Name + (Eq.Connected ? "." : " - connect Equalizer APO above for it to take effect."));
                };
                presetRow.Children.Add(b);
            }
            Status(st.Capture ? "Mic EQ changes what Discord and games hear from you." : "Tip: games often sound clearest with Footsteps (FPS); Discord with Voice clarity.");
        }

        void Changed() { writeTimer.Stop(); writeTimer.Start(); }
        public void Reload() { LoadDevice(); }
    }
}
