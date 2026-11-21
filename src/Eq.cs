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
        [DataMember] public bool Param;        // v1.9: parametric mode (PBands) instead of the 10 graphic bands
        [DataMember] public List<PBand> PBands;
        [DataMember] public int Space;         // v3.0: headphone space (crossfeed) for outputs - 0 off, 1 light, 2 medium, 3 strong
        [DataMember] public MicChain Chain;    // v3.16: gate / compressor / limiter for mics (null = never set up)
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
        public static readonly string[] SpaceNames = { "Off", "Light", "Medium", "Strong" };

        // Headphone space (crossfeed): on headphones, bass and low-mids from one side should reach both ears, like
        // speakers in a room. Done with mid/side so it needs only the device's own L and R channels: the side (L-R)
        // part gets a low shelf, so low sounds are narrowed while treble keeps its full width and anything in the
        // centre is untouched. For a sound hard on one side, the other ear gets the bs2b crossfeed levels
        // (light 9.5 dB, medium 6 dB, strong 4.5 dB quieter) below the shelf frequency.
        public static string[] SpaceLines(int strength)
        {
            if (strength <= 0) return new string[0];
            int s = Math.Min(3, strength);
            double fc = s == 1 ? 650 : 700, feedDb = s == 1 ? 9.5 : s == 2 ? 6.0 : 4.5;
            double g = Math.Pow(10, -feedDb / 20), side = (1 - g) / (1 + g), shelf = 20 * Math.Log10(side);
            Func<double, string> f = x => x.ToString("0.###", CultureInfo.InvariantCulture);
            return new[] {
                "# Headphone space: " + SpaceNames[s] + " (crossfeed below " + f(fc) + " Hz, other ear " + f(feedDb) + " dB down)",
                "Copy: L=0.5*L+0.5*R R=0.5*L+-0.5*R",
                "Channel: R",
                "Filter: ON LSC Fc " + f(fc) + " Hz Gain " + f(shelf) + " dB Q 0.707",
                "Channel: L R",
                "Copy: L=L+R R=L+-1.0*R",
                "Channel: all" };
        }

        // One click: the Discord-ready mic sound on this mic (voice EQ + 80 Hz low cut).
        public static EqState MakeDiscordReady(Dictionary<string, EqState> eq, string micId, string micName)
        {
            EqState st;
            if (!eq.TryGetValue(micId, out st)) { st = new EqState(); eq[micId] = st; }
            st.Name = "Mic: " + micName; st.Capture = true; st.On = true; st.Preset = "Discord-ready"; st.Param = false;
            st.Gains = (int[])MicPresets["Discord-ready"].Clone(); st.LowCut = 80;
            Write(eq);
            return st;
        }

        public static EqState Clone(EqState e)
        {
            return new EqState { Name = e.Name, Capture = e.Capture, On = e.On, Preset = e.Preset, LowCut = e.LowCut, Space = e.Space, Gains = e.Gains == null ? new int[10] : (int[])e.Gains.Clone(),
                Param = e.Param, PBands = e.PBands == null ? null : PEq.Copy(e.PBands), Chain = e.Chain == null ? null : e.Chain.Copy() };
        }
        static string Guid(string deviceId) { int i = deviceId.LastIndexOf('.'); return i >= 0 ? deviceId.Substring(i + 1) : deviceId; }

        // Writes every device's EQ. Equalizer APO picks the change up within a second.
        public static void Write(Dictionary<string, EqState> eq)
        {
            if (Snap.On) return;
            var text = Text(eq);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile));
                File.WriteAllText(ConfigFile, text, new UTF8Encoding(false));
            }
            catch { }
        }
        // The text of our Equalizer APO file.
        public static string Text(Dictionary<string, EqState> eq)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Written by Hallow Mixer - change it in Hallow Mixer > EQ (edits here are overwritten).");
            foreach (var kv in eq)
            {
                var e = kv.Value; if (e.Gains == null) e.Gains = new int[10];
                bool param = e.Param && e.PBands != null;
                var act = param ? e.PBands.Where(PEq.Active).ToList() : null;
                int space = e.Capture ? 0 : e.Space;
                bool eqOn = e.On && !((param ? act.Count == 0 : e.Gains.All(g => g == 0)) && e.LowCut <= 0 && space <= 0);
                string chain = e.Capture && e.Chain != null && e.Chain.On ? e.Chain.ConfigLine() : null;   // v3.16 mic chain, after the EQ
                if (!eqOn && chain == null) continue;
                sb.AppendLine();
                sb.AppendLine("# " + (e.Name ?? kv.Key) + " - " + (eqOn ? e.Preset + (param ? " (parametric)" : "") : "no EQ") + (chain != null ? " + mic chain " + e.Chain.Preset : ""));
                sb.AppendLine("Device: " + Guid(kv.Key));
                if (!eqOn) { sb.AppendLine(chain); continue; }
                double max = param ? Math.Ceiling(PEq.MaxBoost(act) * 10) / 10 : Math.Max(0, e.Gains.Max());
                sb.AppendLine("Preamp: " + (-max).ToString("0.#", CultureInfo.InvariantCulture) + " dB");   // headroom, so boosts never distort
                if (param) foreach (var b in act) sb.AppendLine(PEq.Line(b));
                else if (e.Gains.Any(g => g != 0))
                    sb.AppendLine("GraphicEQ: " + string.Join("; ", Bands.Select((f, i) => f.ToString(CultureInfo.InvariantCulture) + " " + e.Gains[i].ToString(CultureInfo.InvariantCulture))));
                if (e.LowCut > 0) sb.AppendLine("Filter: ON HP Fc " + e.LowCut.ToString(CultureInfo.InvariantCulture) + " Hz");   // true 12 dB/oct high-pass
                foreach (var line in SpaceLines(space)) sb.AppendLine(line);
                if (chain != null) sb.AppendLine(chain);
            }
            sb.AppendLine();
            sb.AppendLine("Device: all");
            return sb.ToString();
        }
    }

    // The EQ window: pick a device, pick a preset or drag the 10 bands.
    public class EqWindow : Window
    {
        readonly Settings s;
        ComboBox device, lowCut, space; DockPanel lowCutRow, spaceRow; CheckBox onSwitch; WrapPanel presetRow; TextBlock status, apoText; Button connectBtn, configBtn;
        readonly Slider[] bands = new Slider[10]; readonly TextBlock[] vals = new TextBlock[10];
        Button modeG, modeP; Border shell; PEqEditor peq;
        bool loading; readonly DispatcherTimer writeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };

        static SolidColorBrush B(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }

        public EqWindow(Settings settings)
        {
            s = settings;
            Title = "Hallow Mixer - EQ"; Width = 640; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.CanMinimize;
            Background = Look.Bg; Foreground = Look.Text; FontFamily = new FontFamily("Segoe UI"); FontSize = 12.5;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            writeTimer.Tick += (o, e) =>
            {
                writeTimer.Stop(); Eq.Write(s.Eq);
                var st = Cur(); string game = st != null && !st.Capture && OnChanged != null ? OnChanged() : null;
                s.Save();
                if (game != null) Status("Saved for " + game + " - this EQ comes back whenever " + game + " runs.");
            };

            var sp = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            var head = new DockPanel();
            var eqIcon = Icons.Make("sliders", 20, Accent.Ink); eqIcon.Margin = new Thickness(0, 0, 10, 0); var eqT = new StackPanel { Orientation = Orientation.Horizontal }; eqT.Children.Add(eqIcon); eqT.Children.Add(new TextBlock { Text = "EQ", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Accent.Ink }); head.Children.Add(eqT);
            sp.Children.Add(head);
            apoText = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Look.Muted, Margin = new Thickness(0, 4, 0, 8) };
            sp.Children.Add(apoText);
            var ab = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            connectBtn = new Button { Content = "Connect to Equalizer APO", Style = (Style)FindResource("Accent"), Margin = new Thickness(0, 0, 8, 0) };
            connectBtn.Click += (o, e) => { if (Eq.Connect()) { Eq.Write(s.Eq); Status("Connected - your EQ now works."); } else Status("Not connected (the admin prompt was cancelled, or Equalizer APO isn't installed)."); RefreshApo(); };
            configBtn = new Button { Content = "Pick devices (Equalizer APO)", Style = (Style)FindResource("Btn"), ToolTip = "Opens Equalizer APO's Configurator - tick the devices EQ should work on, then restart the PC." };
            configBtn.Click += (o, e) => { var c = Eq.Configurator; if (c != null) try { Process.Start(new ProcessStartInfo(c) { UseShellExecute = true, Verb = "runas" }); } catch { } };
            ab.Children.Add(connectBtn); ab.Children.Add(configBtn);
            sp.Children.Add(ab);

            var dr = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var dl = new TextBlock { Text = "Device", Foreground = Look.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            DockPanel.SetDock(dl, Dock.Left); dr.Children.Add(dl);
            onSwitch = new CheckBox { Content = "EQ on", Style = (Style)FindResource("Switch"), Margin = new Thickness(14, 0, 0, 0) };
            onSwitch.Click += (o, e) => { var st = Cur(); if (st != null) { st.On = onSwitch.IsChecked == true; Changed(); } };
            DockPanel.SetDock(onSwitch, Dock.Right); dr.Children.Add(onSwitch);
            device = new ComboBox { Style = (Style)FindResource("Combo") };
            device.SelectionChanged += (o, e) => LoadDevice();
            dr.Children.Add(device);
            sp.Children.Add(dr);

            var mr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            modeG = new Button { Content = "Graphic EQ", Margin = new Thickness(0, 0, 6, 0), ToolTip = "10 fixed sliders - quick and simple" };
            modeP = new Button { Content = "Parametric EQ", ToolTip = "Place bands anywhere and drag the curve - like Sonar's EQ" };
            modeG.Click += (o, e) => SetMode(false); modeP.Click += (o, e) => SetMode(true);
            mr.Children.Add(modeG); mr.Children.Add(modeP);
            sp.Children.Add(mr);
            presetRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            sp.Children.Add(presetRow);
            myRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };   // v3.19 your own presets, import / export, A/B
            sp.Children.Add(myRow);
            lowCutRow = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            var lcl = new TextBlock { Text = "Low cut", Foreground = Look.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            lowCut = new ComboBox { Style = (Style)FindResource("Combo"), Width = 120, ToolTip = "Removes rumble, desk bumps and fan hum below this pitch (voices stay untouched). 80 Hz suits most voices." };
            lowCut.ItemsSource = Eq.LowCuts.Select(h => h == 0 ? "Off" : h + " Hz").ToList();
            lowCut.SelectionChanged += (o, e) => { if (loading) return; var st = Cur(); if (st == null || lowCut.SelectedIndex < 0) return; st.LowCut = Eq.LowCuts[lowCut.SelectedIndex]; if (st.LowCut > 0) { st.On = true; onSwitch.IsChecked = true; } Changed(); };
            var lch = new TextBlock { Text = "removes rumble and desk bumps", Foreground = Look.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            DockPanel.SetDock(lcl, Dock.Left); DockPanel.SetDock(lowCut, Dock.Left);
            lowCutRow.Children.Add(lcl); lowCutRow.Children.Add(lowCut); lowCutRow.Children.Add(lch);
            sp.Children.Add(lowCutRow);
            sp.Children.Add(BuildChain());
            spaceRow = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            var spl = new TextBlock { Text = "Headphone space", Foreground = Look.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            space = new ComboBox { Style = (Style)FindResource("Combo"), Width = 120, ItemsSource = Eq.SpaceNames,
                ToolTip = "Crossfeed: each ear hears a little of the other side, softened and slightly delayed - like speakers in a room. Less tiring on headphones, and hard-left/right sounds stop feeling stuck inside one ear." };
            System.Windows.Automation.AutomationProperties.SetName(space, "Headphone space");
            space.SelectionChanged += (o, e) => { if (loading) return; var st = Cur(); if (st == null || space.SelectedIndex < 0) return; st.Space = space.SelectedIndex; if (st.Space > 0) { st.On = true; onSwitch.IsChecked = true; } Changed(); };
            var sph = new TextBlock { Text = "headphones only - speakers in a room, not inside your head", Foreground = Look.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            DockPanel.SetDock(spl, Dock.Left); DockPanel.SetDock(space, Dock.Left);
            spaceRow.Children.Add(spl); spaceRow.Children.Add(space); spaceRow.Children.Add(sph);
            sp.Children.Add(spaceRow);

            var grid = new Grid { Height = 210, Background = Look.Panel };
            for (int i = 0; i < 10; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                var col = new DockPanel { Margin = new Thickness(0, 8, 0, 6) };
                var name = new TextBlock { Text = Eq.BandNames[i], HorizontalAlignment = HorizontalAlignment.Center, Foreground = Look.Muted, FontSize = 11 };
                vals[i] = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, FontSize = 11, FontWeight = FontWeights.SemiBold };
                DockPanel.SetDock(name, Dock.Bottom); DockPanel.SetDock(vals[i], Dock.Top);
                col.Children.Add(name); col.Children.Add(vals[i]);
                int band = i;
                bands[i] = new Slider { Style = (Style)FindResource("VFader"), Minimum = -12, Maximum = 12, SmallChange = 1, LargeChange = 3, Foreground = Accent.Brush,
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
            shell = new Border { CornerRadius = new CornerRadius(8), Background = Look.Panel, Child = grid, Padding = new Thickness(6) };
            sp.Children.Add(shell);
            peq = new PEqEditor { Visibility = Visibility.Collapsed };
            peq.Changed = () => { var st = Cur(); if (st == null) return; st.Preset = "Custom"; if (!st.On) { st.On = true; onSwitch.IsChecked = true; } Changed(); };
            sp.Children.Add(peq);
            status = new TextBlock { Foreground = Look.Muted, FontSize = 11.5, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap };
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
            LoadChain(st);
            var curId = CurId(); if (curId != null && !abOther.ContainsKey(curId)) abOther[curId] = Eq.Clone(st);   // A = how it was when you opened it
            FillMine(st);
            lowCut.SelectedIndex = Math.Max(0, Array.IndexOf(Eq.LowCuts, st.LowCut));
            spaceRow.Visibility = st.Capture ? Visibility.Collapsed : Visibility.Visible;
            space.SelectedIndex = Math.Max(0, Math.Min(3, st.Space));
            bool pm = st.Param && st.PBands != null;
            shell.Visibility = pm ? Visibility.Collapsed : Visibility.Visible;
            peq.Visibility = pm ? Visibility.Visible : Visibility.Collapsed;
            modeG.Style = (Style)FindResource(pm ? "Btn" : "Accent"); modeP.Style = (Style)FindResource(pm ? "Accent" : "Btn");
            loading = false;
            presetRow.Children.Clear();
            if (pm)
            {
                peq.Load(st.PBands);
                foreach (var kv in st.Capture ? PEq.MicPresets : PEq.OutPresets)
                {
                    var name = kv.Key; var pb = kv.Value;
                    var b = new Button { Content = name, Style = (Style)FindResource(st.Preset == name ? "Accent" : "Btn"), Margin = new Thickness(0, 0, 6, 6) };
                    b.Click += (o, e) =>
                    {
                        var cur = Cur(); cur.PBands = PEq.Copy(pb); cur.Preset = name; cur.On = true; Changed(); LoadDevice();
                        Status(name + " on " + cur.Name + " - a starting point, drag the dots to taste." + (Eq.Connected ? "" : " Connect Equalizer APO above for it to take effect."));
                    };
                    presetRow.Children.Add(b);
                }
                var flat = new Button { Content = "Reset", Style = (Style)FindResource("Btn"), Margin = new Thickness(0, 0, 6, 6), ToolTip = "Back to 5 empty bands" };
                flat.Click += (o, e) => { var cur = Cur(); cur.PBands = PEq.FromGraphic(null); cur.Preset = "Flat"; Changed(); LoadDevice(); };
                presetRow.Children.Add(flat);
                Status(st.Capture ? "Mic EQ changes what Discord and games hear from you." : "Game presets lift footsteps and reloads and trim boomy lows.");
                return;
            }
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

        // ---- v3.19 your own presets, import / export / paste, A/B compare
        WrapPanel myRow;
        readonly Dictionary<string, EqState> abOther = new Dictionary<string, EqState>(); readonly HashSet<string> abOnA = new HashSet<string>();
        string CurId() { var d = device.SelectedItem as AudioDevice; return d == null ? null : d.Id; }
        Button MyBtn(string text, string tip, Action click)
        {
            var b = new Button { Content = text, Style = (Style)FindResource("Btn"), Margin = new Thickness(0, 0, 6, 6), ToolTip = tip };
            System.Windows.Automation.AutomationProperties.SetName(b, text);
            b.Click += (o, e) => click(); myRow.Children.Add(b); return b;
        }
        void FillMine(EqState st)
        {
            myRow.Children.Clear();
            myRow.Children.Add(new TextBlock { Text = "Mine", Foreground = Look.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 6) });
            foreach (var p in s.MyEq.Where(x => x.Capture == st.Capture).ToList())
            {
                var preset = p;
                var b = MyBtn(p.Name, "Your preset - click to use it. Right-click to export or delete it.", () =>
                {
                    var cur = Cur(); if (cur == null) return;
                    EqFiles.ApplyTo(preset, cur); Changed(); LoadDevice();
                    Status(preset.Name + " on " + cur.Name + (Eq.Connected ? "." : " - connect Equalizer APO above for it to take effect."));
                });
                if (st.Preset == p.Name) b.Style = (Style)FindResource("Accent");
                var cm = new ContextMenu();
                var ex = new MenuItem { Header = "Export..." }; ex.Click += (o, e) => Export(preset); cm.Items.Add(ex);
                var del = new MenuItem { Header = "Delete" }; del.Click += (o, e) => { s.MyEq.Remove(preset); s.Save(); FillMine(Cur()); Status("Deleted your preset " + preset.Name + "."); }; cm.Items.Add(del);
                b.ContextMenu = cm;
            }
            MyBtn("Save as...", "Save this EQ as your own preset", () =>
            {
                var cur = Cur(); if (cur == null) return;
                var n = MixerWindow.AskIn(this, "Save EQ preset", "Name for this EQ:", MicChain.IsPreset(cur.Preset) || string.IsNullOrEmpty(cur.Preset) || cur.Preset == "Custom" || cur.Preset == "Flat" ? "My EQ" : cur.Preset);
                if (string.IsNullOrWhiteSpace(n)) return;
                var p = EqFiles.From(cur, n.Trim());
                s.MyEq.RemoveAll(x => x.Capture == p.Capture && string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase));
                s.MyEq.Add(p); cur.Preset = p.Name; s.Save(); FillMine(cur);
                Status("Saved as " + p.Name + ". Right-click it to export it for a friend.");
            });
            MyBtn("Export...", "Save this EQ to a file - a Hallow Mixer preset, or Equalizer APO text for other apps", () => { var cur = Cur(); if (cur != null) Export(EqFiles.From(cur, string.IsNullOrEmpty(cur.Preset) ? "My EQ" : cur.Preset)); });
            MyBtn("Import...", "Open a Hallow Mixer preset, AutoEQ ParametricEQ.txt / GraphicEQ.txt or Equalizer APO text", () =>
            {
                var d = new Microsoft.Win32.OpenFileDialog { Filter = "EQ presets (*.json;*.txt)|*.json;*.txt|All files (*.*)|*.*" };
                if (d.ShowDialog(this) != true) return;
                string text; try { if (new FileInfo(d.FileName).Length > 512 * 1024) { Status("That file is too big for an EQ preset."); return; } text = File.ReadAllText(d.FileName); } catch (Exception ex) { Status("Couldn't read it: " + ex.Message); return; }
                UseImported(text, Path.GetFileNameWithoutExtension(d.FileName));
            });
            MyBtn("Paste", "Paste EQ text you copied - AutoEQ's ParametricEQ.txt, GraphicEQ.txt or Equalizer APO Filter: lines", () =>
            {
                string text = null; try { text = Clipboard.GetText(); } catch { }
                if (string.IsNullOrWhiteSpace(text)) { Status("Copy the EQ text first (for example everything in AutoEQ's ParametricEQ.txt), then click Paste."); return; }
                var n = MixerWindow.AskIn(this, "Pasted EQ", "Name for this EQ (for example your headphones):", "Pasted EQ");
                if (string.IsNullOrWhiteSpace(n)) return;
                UseImported(text, n.Trim());
            });
            var id = CurId();
            bool onA = id != null && abOnA.Contains(id);
            var ab = MyBtn(onA ? "A/B: hearing before" : "A/B: hearing now", "Switch between this EQ and how it was when you opened the window - to hear the difference", () =>
            {
                var i = CurId(); var cur = Cur(); EqState other; if (i == null || cur == null || !abOther.TryGetValue(i, out other)) return;
                abOther[i] = Eq.Clone(cur); s.Eq[i] = Eq.Clone(other);
                if (!abOnA.Remove(i)) abOnA.Add(i);
                Changed(); LoadDevice();
                Status(abOnA.Contains(i) ? "A: how it was before. Click A/B again for your changes." : "B: your changes.");
            });
            if (onA) ab.Style = (Style)FindResource("Accent");
        }
        void UseImported(string text, string name)
        {
            var cur = Cur(); if (cur == null) return;
            string note; var p = EqFiles.Parse(text, name, out note);
            if (p == null) { Status(note); return; }
            p.Capture = cur.Capture;
            EqFiles.ApplyTo(p, cur);
            s.MyEq.RemoveAll(x => x.Capture == p.Capture && string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase));
            s.MyEq.Add(p); s.Save(); Changed(); LoadDevice();
            Status(p.Name + " is on " + cur.Name + " and saved under Mine" + (p.Param ? " (parametric, " + p.PBands.Count + " bands; the preamp is set for you)" : "") + "." + (note != null ? " " + note : "") + (Eq.Connected ? "" : " Connect Equalizer APO above for it to take effect."));
        }
        void Export(EqUserPreset p)
        {
            var safe = new string((p.Name ?? "EQ").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
            var d = new Microsoft.Win32.SaveFileDialog { FileName = safe, Filter = "Hallow Mixer EQ preset (*.json)|*.json|Equalizer APO text (*.txt)|*.txt", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) };
            if (d.ShowDialog(this) != true) return;
            try { File.WriteAllText(d.FileName, d.FilterIndex == 2 ? EqFiles.ToApoText(p) : EqFiles.ToJson(p), new UTF8Encoding(false)); Status("Saved " + p.Name + " to " + d.FileName); }
            catch (Exception ex) { Status("Couldn't save it: " + ex.Message); }
        }

        // ---- v3.16 mic chain: noise gate -> compressor -> limiter after the mic's EQ (Hallow Mic Chain plugin in Equalizer APO)
        Border chainPanel; CheckBox chainOn, gateOn, compOn, limOn; WrapPanel chainPresets; bool chainLoading;
        Slider gateThr, gateRange, compThr, compRatio, compMakeup, limCeil;
        MicChain ChainOf(EqState st) { if (st.Chain == null) st.Chain = MicChain.Make("Off"); return st.Chain; }
        void ChainEdit(Action<MicChain> change)
        {
            if (chainLoading) return;
            var st = Cur(); if (st == null || !st.Capture) return;
            var c = ChainOf(st); change(c); c.Preset = "Custom"; Changed(); LoadChain(st);
        }
        UIElement BuildChain()
        {
            var sp = new StackPanel();
            var top = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            chainOn = new CheckBox { Content = "Mic chain", Style = (Style)FindResource("Switch"), FontWeight = FontWeights.SemiBold,
                ToolTip = "Noise gate, compressor and limiter on your mic, after the EQ - steadier voice, no background hiss between words, never too loud" };
            chainOn.Click += (o, e) =>
            {
                var st = Cur(); if (st == null) return;
                var c = ChainOf(st);
                if (chainOn.IsChecked == true && c.Preset == "Off") st.Chain = MicChain.Make("Light");   // first time: a gentle start
                else c.On = chainOn.IsChecked == true;                                                    // keeps your settings for next time
                Changed(); LoadChain(st);
                Status(st.Chain.On ? "Mic chain on (" + st.Chain.Preset + ")" + (Eq.Connected ? "." : " - connect Equalizer APO above for it to take effect.") : "Mic chain off.");
            };
            DockPanel.SetDock(chainOn, Dock.Left); top.Children.Add(chainOn);
            top.Children.Add(new TextBlock { Text = "gate  >  compressor  >  limiter, after the EQ", Foreground = Look.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) });
            sp.Children.Add(top);
            chainPresets = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            foreach (var n in MicChain.PresetNames)
            {
                var name = n;
                var b = new Button { Content = name, Style = (Style)FindResource("Btn"), Margin = new Thickness(0, 0, 6, 6), Tag = name,
                    ToolTip = name == "Off" ? "No gate, compressor or limiter" : name == "Light" ? "Gentle gate and compressor - a steadier voice" : name == "Streamer" ? "Stronger compressor and limiter - even and loud, never clipping" : "Stronger gate - keyboards, fans and the room stay out between words" };
                b.Click += (o, e) =>
                {
                    var st = Cur(); if (st == null) return;
                    st.Chain = MicChain.Make(name); Changed(); LoadChain(st);
                    Status(name == "Off" ? "Mic chain off." : name + " mic chain on " + st.Name + (Eq.Connected ? "." : " - connect Equalizer APO above for it to take effect."));
                };
                chainPresets.Children.Add(b);
            }
            sp.Children.Add(chainPresets);
            var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition());
            Func<string, string, CheckBox> stage = (text, tip) => new CheckBox { Content = text, Style = (Style)FindResource("Switch"), ToolTip = tip, Margin = new Thickness(0, 0, 0, 4) };
            Func<StackPanel, string, double, double, string, Action<MicChain, float>, Slider> slider = (col, label, lo, hi, unit, set) =>
            {
                var t = new TextBlock { FontSize = 11, Foreground = Look.Muted };
                var sl = new Slider { Style = (Style)FindResource("HFader"), Minimum = lo, Maximum = hi, Foreground = B("#F05A8C"), Height = 22, Margin = new Thickness(0, 0, 0, 4) };
                System.Windows.Automation.AutomationProperties.SetName(sl, label);
                Action text = () => { t.Text = label + "  " + sl.Value.ToString(unit == ":1" ? "0.0" : "0") + (unit == ":1" ? ":1" : " " + unit); };
                sl.Tag = text;
                sl.ValueChanged += (o, e) =>
                {
                    text();
                    var v = (float)Math.Round(sl.Value, unit == ":1" ? 1 : 0); ChainEdit(c => set(c, v));
                };
                col.Children.Add(t); col.Children.Add(sl); return sl;
            };
            var gc = new StackPanel { Margin = new Thickness(0, 0, 14, 0) }; var cc = new StackPanel { Margin = new Thickness(0, 0, 14, 0) }; var lc = new StackPanel();
            gateOn = stage("Noise gate", "Turns the mic down between words (keyboard, fan, room)"); gateOn.Click += (o, e) => ChainEdit(c => c.GateOn = gateOn.IsChecked == true); gc.Children.Add(gateOn);
            gateThr = slider(gc, "Opens above", -80, -10, "dB", (c, v) => c.GateThr = v);
            gateRange = slider(gc, "Turns down by", -90, 0, "dB", (c, v) => c.GateRange = v);
            compOn = stage("Compressor", "Evens out loud and quiet talking"); compOn.Click += (o, e) => ChainEdit(c => c.CompOn = compOn.IsChecked == true); cc.Children.Add(compOn);
            compThr = slider(cc, "Threshold", -60, 0, "dB", (c, v) => c.CompThr = v);
            compRatio = slider(cc, "Ratio", 1, 10, ":1", (c, v) => c.CompRatio = v);
            compMakeup = slider(cc, "Makeup", 0, 24, "dB", (c, v) => c.CompMakeup = v);
            limOn = stage("Limiter", "Never louder than the ceiling - no clipping when you shout"); limOn.Click += (o, e) => ChainEdit(c => c.LimOn = limOn.IsChecked == true); lc.Children.Add(limOn);
            limCeil = slider(lc, "Ceiling", -24, 0, "dB", (c, v) => c.LimCeil = v);
            Grid.SetColumn(cc, 1); Grid.SetColumn(lc, 2); g.Children.Add(gc); g.Children.Add(cc); g.Children.Add(lc);
            sp.Children.Add(g);
            chainPanel = new Border { Background = Look.Panel, CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 10, 14, 8), Margin = new Thickness(0, 0, 0, 12), Child = sp, Visibility = Visibility.Collapsed };
            return chainPanel;
        }
        void LoadChain(EqState st)
        {
            if (chainPanel == null) return;
            chainPanel.Visibility = st != null && st.Capture ? Visibility.Visible : Visibility.Collapsed;
            if (st == null || !st.Capture) return;
            var c = st.Chain ?? MicChain.Make("Off");
            chainLoading = true;
            chainOn.IsChecked = c.On; gateOn.IsChecked = c.GateOn; compOn.IsChecked = c.CompOn; limOn.IsChecked = c.LimOn;
            gateThr.Value = c.GateThr; gateRange.Value = c.GateRange; compThr.Value = c.CompThr; compRatio.Value = c.CompRatio; compMakeup.Value = c.CompMakeup; limCeil.Value = c.LimCeil;
            foreach (var sl in new[] { gateThr, gateRange, compThr, compRatio, compMakeup, limCeil }) ((Action)sl.Tag)();
            foreach (Button b in chainPresets.Children) b.Style = (Style)FindResource((string)b.Tag == (c.On ? c.Preset : "Off") ? "Accent" : "Btn");
            bool en = c.On; gateOn.IsEnabled = compOn.IsEnabled = limOn.IsEnabled = en;
            gateThr.IsEnabled = gateRange.IsEnabled = en && c.GateOn; compThr.IsEnabled = compRatio.IsEnabled = compMakeup.IsEnabled = en && c.CompOn; limCeil.IsEnabled = en && c.LimOn;
            chainLoading = false;
        }
        void SetMode(bool param)
        {
            var st = Cur(); if (st == null || st.Param == param) return;
            if (param && (st.PBands == null || st.PBands.Count == 0)) st.PBands = PEq.FromGraphic(st.Gains);
            st.Param = param; Changed(); LoadDevice();
            Status(param ? "Parametric EQ - your graphic EQ was carried over. Drag the dots, or pick a preset." : "Graphic EQ - your parametric bands are kept for when you switch back.");
        }
        public void Reload() { LoadDevice(); }
        // --snap: the mic with the Streamer chain, to check the layout (settings are frozen in snaps)
        public void SnapMic()
        {
            var mic = (device.ItemsSource as List<AudioDevice>).FirstOrDefault(d => d.Name.StartsWith("Mic: ")); if (mic == null) return;
            device.SelectedItem = mic; var st = Cur(); if (st != null) { st.Chain = MicChain.Make("Streamer"); LoadDevice(); }
        }
        public Func<string> OnChanged;   // returns the running game's name when the change was saved for it
    }
}
