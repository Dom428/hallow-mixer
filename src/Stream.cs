// Hallow Mixer - the Stream window: OBS connection, a separate stream mix per channel, and presets that follow OBS scenes.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace HallowMixer
{
    public class StreamWindow : Window
    {
        readonly Engine eng; readonly ObsLink obs; readonly Func<bool, Task> connect;
        TextBlock state, status; TextBox host, port; PasswordBox pass; Button connectBtn, setupBtn;
        StackPanel mixRows, sceneRows; CheckBox autoSw;
        List<string> inputs = new List<string>(), scenes = new List<string>();
        readonly DispatcherTimer applyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        static readonly string[] Chans = { "game", "chat", "music", "media", "other", "mic" };
        static readonly Dictionary<string, string> ChanColor = new Dictionary<string, string> {
            { "game", "#F07820" }, { "chat", "#A88BEB" }, { "music", "#6CCB7A" }, { "media", "#5AB4F0" }, { "other", "#9E97AD" }, { "mic", "#F06292" } };
        static SolidColorBrush B(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        static string ChanName(string id) { return id == "mic" ? "Mic" : id == "other" ? "System" : Engine.ChannelNames[id]; }

        public StreamWindow(Engine engine, ObsLink link, Func<bool, Task> connectFn)
        {
            eng = engine; obs = link; connect = connectFn;
            Title = "Hallow Mixer - Stream"; Width = 640; SizeToContent = SizeToContent.Height; MaxHeight = 820; ResizeMode = ResizeMode.CanMinimize;
            Background = Look.Bg; Foreground = Look.Text; FontFamily = new FontFamily("Segoe UI"); FontSize = 12.5;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            applyTimer.Tick += async (o, e) => { applyTimer.Stop(); eng.S.Save(); if (obs.Connected) await StreamMix.Apply(obs, eng.S, null); };

            var sp = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            sp.Children.Add(new TextBlock { Text = "Stream", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Accent.Ink });
            sp.Children.Add(new TextBlock { Text = "A separate mix for your stream through OBS: viewers can hear less music, louder chat or no game - without changing what you hear.",
                TextWrapping = TextWrapping.Wrap, Foreground = Look.Muted, Margin = new Thickness(0, 4, 0, 12) });

            // ---- connection
            var cr = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            state = new TextBlock { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            connectBtn = new Button { Style = (Style)FindResource("Accent"), MinWidth = 100 };
            connectBtn.Click += async (o, e) => await ToggleConnect();
            DockPanel.SetDock(connectBtn, Dock.Right); cr.Children.Add(connectBtn); cr.Children.Add(state);
            sp.Children.Add(cr);
            var fr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            Func<string, UIElement, FrameworkElement> field = (label, box) =>
            {
                var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 0) };
                p.Children.Add(new TextBlock { Text = label, Foreground = Look.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
                p.Children.Add(box); return p;
            };
            host = Box(eng.S.ObsHost, 120); port = Box(eng.S.ObsPort.ToString(), 60);
            pass = new PasswordBox { Width = 150, Background = Look.Field, Foreground = Look.Text, BorderBrush = Look.Line, Padding = new Thickness(6, 4, 6, 4), CaretBrush = Look.Text };
            pass.Password = ObsLink.Unprotect(eng.S.ObsPass);
            fr.Children.Add(field("Address", host)); fr.Children.Add(field("Port", port)); fr.Children.Add(field("Password", pass));
            sp.Children.Add(fr);
            autoSw = new CheckBox { Content = "Connect to OBS by itself (when Hallow Mixer starts and whenever OBS opens)", IsChecked = eng.S.ObsAuto, Style = (Style)FindResource("Switch") };
            autoSw.Click += (o, e) => { eng.S.ObsAuto = autoSw.IsChecked == true; eng.S.Save(); };
            sp.Children.Add(autoSw);
            sp.Children.Add(new TextBlock { Text = "In OBS: Tools > WebSocket Server Settings > tick Enable WebSocket server. Show Connect Info shows the password.",
                TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Look.Muted, Margin = new Thickness(0, 2, 0, 14) });

            // ---- stream mix
            var mh = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            mh.Children.Add(new TextBlock { Text = "Stream mix", FontSize = 15, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            setupBtn = new Button { Content = "Set up OBS for me", Style = (Style)FindResource("Accent"), ToolTip = "Makes an Application Audio Capture source in OBS for each app in Game, Chat, Music and Media, and links it here" };
            setupBtn.Click += async (o, e) => await AutoSetup();
            DockPanel.SetDock(setupBtn, Dock.Right); mh.Children.Insert(0, setupBtn);
            sp.Children.Add(mh);
            mixRows = new StackPanel();
            sp.Children.Add(new Border { Background = Look.Panel, CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Child = mixRows });

            // ---- scenes
            sp.Children.Add(new TextBlock { Text = "Presets per scene", FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 16, 0, 2) });
            sp.Children.Add(new TextBlock { Text = "When OBS switches to a scene, your levels switch to its preset (e.g. Just Chatting = Chill, BRB = music up).",
                TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Look.Muted, Margin = new Thickness(0, 0, 0, 8) });
            sceneRows = new StackPanel();
            sp.Children.Add(new Border { Background = Look.Panel, CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Child = sceneRows });

            status = new TextBlock { Foreground = Look.Muted, FontSize = 11.5, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap };
            sp.Children.Add(status);
            Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Loaded += async (o, e) => await Refresh();
        }

        TextBox Box(string text, double w)
        {
            return new TextBox { Text = text, Width = w, Background = Look.Field, Foreground = Look.Text, BorderBrush = Look.Line, Padding = new Thickness(6, 4, 6, 4), CaretBrush = Look.Text };
        }
        public void Status(string t) { status.Text = t; }

        async Task ToggleConnect()
        {
            if (obs.Connected) { obs.Close(); await Refresh(); Status("Disconnected from OBS."); return; }
            int p; if (!int.TryParse(port.Text.Trim(), out p) || p <= 0 || p > 65535) { Status("The port is a number - OBS uses 4455 unless you changed it."); return; }
            eng.S.ObsHost = string.IsNullOrWhiteSpace(host.Text) ? "localhost" : host.Text.Trim(); eng.S.ObsPort = p; eng.S.ObsPass = ObsLink.Protect(pass.Password); eng.S.Save();
            connectBtn.IsEnabled = false; connectBtn.Content = "Connecting...";
            await connect(false);
            connectBtn.IsEnabled = true;
            await Refresh();
            Status(obs.Connected ? "Connected. Link OBS sources to each channel below, or click Set up OBS for me." : obs.LastError);
        }

        async Task AutoSetup()
        {
            if (!obs.Connected) { Status("Connect to OBS first."); return; }
            setupBtn.IsEnabled = false; Status("Setting up OBS...");
            try { Status(await StreamMix.AutoSetup(obs, eng)); } catch (Exception ex) { Status("Couldn't set up OBS: " + ex.Message); }
            setupBtn.IsEnabled = true;
            await Refresh();
        }

        // Re-reads OBS (sources, scenes) and rebuilds the lists.
        public async Task Refresh()
        {
            state.Text = obs.Connected ? "Connected to OBS (WebSocket " + obs.ObsVersion + ")" : "Not connected to OBS";
            state.Foreground = obs.Connected ? Look.GreenText : Look.Muted;
            connectBtn.Content = obs.Connected ? "Disconnect" : "Connect";
            setupBtn.IsEnabled = obs.Connected;
            inputs = new List<string>(); scenes = new List<string>();
            if (obs.Connected)
            {
                try { inputs = await obs.AudioInputs(); scenes = await obs.Scenes(); }
                catch (Exception ex) { Status("OBS: " + ex.Message); }
            }
            BuildMix(); BuildScenes();
        }

        void BuildMix()
        {
            mixRows.Children.Clear();
            foreach (var ch in Chans)
            {
                var id = ch;
                var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                foreach (var w in new[] { 96.0, 92, -1, 44, 150 }) g.ColumnDefinitions.Add(new ColumnDefinition { Width = w < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(w) });
                var name = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                name.Children.Add(new Border { Width = 4, Height = 16, CornerRadius = new CornerRadius(2), Background = B(ChanColor[id]), Margin = new Thickness(0, 0, 8, 0) });
                name.Children.Add(new TextBlock { Text = ChanName(id), FontWeight = FontWeights.SemiBold });
                g.Children.Add(name);
                var on = new CheckBox { Content = "On", IsChecked = StreamMix.OnStream(eng.S, id), Style = (Style)FindResource("Switch"), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Off = viewers don't hear this channel at all" };
                Grid.SetColumn(on, 1); g.Children.Add(on);
                var sl = new Slider { Style = (Style)FindResource("HFader"), Minimum = 0, Maximum = 100, Value = StreamMix.Level(eng.S, id), Foreground = B(ChanColor[id]), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                Grid.SetColumn(sl, 2); g.Children.Add(sl);
                var pct = new TextBlock { Text = StreamMix.Level(eng.S, id) + "%", VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
                Grid.SetColumn(pct, 3); g.Children.Add(pct);
                on.Click += (o, e) => { if (on.IsChecked == true) eng.S.StreamOff.Remove(id); else if (!eng.S.StreamOff.Contains(id)) eng.S.StreamOff.Add(id); Kick(); };
                sl.ValueChanged += (o, e) => { var v = (int)Math.Round(sl.Value); eng.S.StreamLevel[id] = v; pct.Text = v + "%"; Kick(); };
                sl.PreviewMouseWheel += (o, e) => { sl.Value = Math.Max(0, Math.Min(100, Math.Round(sl.Value) + (e.Delta > 0 ? 5 : -5))); e.Handled = true; };
                var links = StreamMix.Links(eng.S, id);
                var pick = new Button { Style = (Style)FindResource("Btn"), Margin = new Thickness(8, 0, 0, 0), HorizontalContentAlignment = HorizontalAlignment.Left,
                    Content = links.Count == 0 ? "Link OBS sources" : links.Count == 1 ? links[0] : links.Count + " OBS sources",
                    ToolTip = links.Count == 0 ? "Pick which OBS sources this channel controls" : string.Join("\n", links) };
                pick.Click += (o, e) => PickSources(id, pick);
                Grid.SetColumn(pick, 4); g.Children.Add(pick);
                mixRows.Children.Add(g);
            }
        }

        void PickSources(string ch, Button anchor)
        {
            if (!obs.Connected) { Status("Connect to OBS to pick its sources."); return; }
            var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom };
            var links = StreamMix.Links(eng.S, ch);
            var all = inputs.Union(links).ToList();
            if (all.Count == 0) menu.Items.Add(new MenuItem { Header = "OBS has no sound sources yet - try Set up OBS for me", IsEnabled = false });
            foreach (var name in all)
            {
                var n = name;
                var mi = new MenuItem { Header = inputs.Contains(n) ? n : n + "  (not in OBS any more)", IsCheckable = true, IsChecked = links.Contains(n), StaysOpenOnClick = true };
                mi.Click += (o, e) =>
                {
                    List<string> l; if (!eng.S.StreamLinks.TryGetValue(ch, out l)) { l = new List<string>(); eng.S.StreamLinks[ch] = l; }
                    if (mi.IsChecked) { foreach (var other in eng.S.StreamLinks.Values) other.Remove(n); l.Add(n); } else l.Remove(n);   // a source belongs to one channel
                    Kick();
                };
                menu.Items.Add(mi);
            }
            menu.Closed += (o, e) => BuildMix();
            menu.IsOpen = true;
        }

        void BuildScenes()
        {
            sceneRows.Children.Clear();
            if (scenes.Count == 0)
            {
                sceneRows.Children.Add(new TextBlock { Text = obs.Connected ? "No scenes found in OBS." : "Connect to OBS to see your scenes.", Foreground = Look.Muted });
                return;
            }
            var names = new List<string> { "(keep my levels)" }; names.AddRange(eng.S.Presets.Select(p => p.Name));
            foreach (var sc in scenes)
            {
                var scene = sc;
                var d = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
                var cb = new ComboBox { Style = (Style)FindResource("Combo"), Width = 200, ItemsSource = names };
                string cur; cb.SelectedIndex = eng.S.ScenePresets.TryGetValue(scene, out cur) ? Math.Max(0, names.IndexOf(cur)) : 0;
                cb.SelectionChanged += (o, e) =>
                {
                    if (cb.SelectedIndex <= 0) eng.S.ScenePresets.Remove(scene); else eng.S.ScenePresets[scene] = names[cb.SelectedIndex];
                    eng.S.Save(); Status(cb.SelectedIndex <= 0 ? scene + ": levels stay as they are." : scene + " -> " + names[cb.SelectedIndex] + " preset.");
                };
                DockPanel.SetDock(cb, Dock.Right); d.Children.Add(cb);
                d.Children.Add(new TextBlock { Text = scene, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                sceneRows.Children.Add(d);
            }
        }

        void Kick() { applyTimer.Stop(); applyTimer.Start(); }
    }
}
