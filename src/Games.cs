// Hallow Mixer - game profiles: every game Hallow Mixer remembers (its levels and its EQ) in one place.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace HallowMixer
{
    public class GamesWindow : Window
    {
        readonly Engine eng; readonly Action changed;
        readonly StackPanel list = new StackPanel();
        readonly DispatcherTimer saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        static readonly Dictionary<string, string> Hex = new Dictionary<string, string> {
            { "game", "#F07820" }, { "chat", "#A98BE0" }, { "music", "#6ED28C" }, { "media", "#5AB4F0" }, { "other", "#8F88A0" } };
        static SolidColorBrush B(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }

        public GamesWindow(Engine engine, Action onChanged)
        {
            eng = engine; changed = onChanged;
            Title = "Hallow Mixer - games"; Width = 760; Height = 600; MinWidth = 640; MinHeight = 360;
            Background = (Brush)FindResource("Bg"); Foreground = (Brush)FindResource("Text");
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12.5;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var dp = new DockPanel { Margin = new Thickness(22, 16, 22, 16) };
            var head = new StackPanel(); DockPanel.SetDock(head, Dock.Top);
            head.Children.Add(new TextBlock { Text = "Game profiles", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = B("#F07820") });
            head.Children.Add(new TextBlock { Text = "Each game's own levels and headset EQ come back when it starts. Change them here, or forget a game to start fresh.",
                Foreground = (Brush)FindResource("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 12) });
            dp.Children.Add(head);
            dp.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            Content = dp;
            saveTimer.Tick += (s, e) => { saveTimer.Stop(); eng.S.Save(); };
            Closed += (s, e) => { if (saveTimer.IsEnabled) { saveTimer.Stop(); eng.S.Save(); } };
            Fill();
        }

        public void Fill()
        {
            list.Children.Clear();
            var keys = eng.S.GameLevels.Keys.Union(eng.S.GameEq.Keys).Distinct()
                .OrderByDescending(k => k == eng.CurrentGame).ThenBy(k => NameOf(k), StringComparer.OrdinalIgnoreCase).ToList();
            if (keys.Count == 0)
            {
                list.Children.Add(new TextBlock { Text = eng.S.PerGame || eng.S.EqPerGame
                    ? "No games yet. Start a game: Hallow Mixer remembers its levels (and EQ) from then on."
                    : "Remembering levels and EQ per game is off - turn it on in Settings.",
                    Foreground = (Brush)FindResource("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 20, 0, 0) });
                return;
            }
            foreach (var k in keys) list.Children.Add(Card(k));
        }

        string NameOf(string key)
        {
            Preset p; if (eng.S.GameLevels.TryGetValue(key, out p) && !string.IsNullOrEmpty(p.Name)) return p.Name;
            if (key == eng.CurrentGame && eng.CurrentGameName != null) return eng.CurrentGameName;
            return key.Length > 0 ? char.ToUpperInvariant(key[0]) + key.Substring(1) : key;
        }

        Button Btn(string text, string tip, Action click)
        {
            var b = new Button { Content = text, Style = (Style)FindResource("Btn"), Margin = new Thickness(6, 0, 0, 0), ToolTip = tip };
            System.Windows.Automation.AutomationProperties.SetName(b, text);
            b.Click += (s, e) => click(); return b;
        }

        UIElement Card(string key)
        {
            var name = NameOf(key); bool now = key == eng.CurrentGame;
            var card = new Border { Background = (Brush)FindResource("Panel"), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, 0, 10) };
            System.Windows.Automation.AutomationProperties.SetName(card, name);
            var sp = new StackPanel();
            var top = new DockPanel();
            var btns = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(btns, Dock.Right);
            Preset lv; bool hasLv = eng.S.GameLevels.TryGetValue(key, out lv);
            Dictionary<string, EqState> geq; bool hasEq = eng.S.GameEq.TryGetValue(key, out geq) && geq != null && geq.Count > 0;
            btns.Children.Add(Btn("Use my current levels", "Save the mixer's current levels as " + name + "'s levels", () => { eng.SetGameLevels(key, eng.CurrentLevels(name)); changed(); Fill(); }));
            btns.Children.Add(Btn("Forget", "Forget " + name + "'s levels and EQ - it starts fresh next time", () =>
            {
                if (MessageBox.Show(this, "Forget " + name + "'s saved levels and EQ?", "Hallow Mixer", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                eng.ForgetGame(key, true, true); changed(); Fill();
            }));
            top.Children.Add(btns);
            var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            title.Children.Add(new TextBlock { Text = name, FontSize = 15, FontWeight = FontWeights.Bold });
            if (now) title.Children.Add(new Border { Background = B("#F07820"), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = "PLAYING NOW", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = B("#16121C") } });
            top.Children.Add(title);
            sp.Children.Add(top);

            // levels: one small slider per channel
            if (!hasLv) lv = null;
            var grid = new UniformGrid5();
            foreach (var ch in Engine.ChannelIds)
            {
                var id = ch; int v = lv != null && lv.Volumes.ContainsKey(id) ? lv.Volumes[id] : 100;
                var cell = new StackPanel { Margin = new Thickness(0, 10, 12, 0) };
                var lab = new TextBlock { Text = (id == "other" ? "System" : Engine.ChannelNames[id]) + "  " + v + "%", FontSize = 11.5, Foreground = B(Hex[id]) };
                var sl = new Slider { Style = (Style)FindResource("HFader"), Minimum = 0, Maximum = 100, Value = v, Foreground = B(Hex[id]), Height = 22, IsEnabled = hasLv };
                System.Windows.Automation.AutomationProperties.SetName(sl, name + " " + Engine.ChannelNames[id]);
                sl.ValueChanged += (s, e) =>
                {
                    int nv = (int)Math.Round(sl.Value);
                    lab.Text = (id == "other" ? "System" : Engine.ChannelNames[id]) + "  " + nv + "%";
                    Preset cur; if (!eng.S.GameLevels.TryGetValue(key, out cur)) return;
                    cur.Volumes[id] = nv; eng.SetGameLevels(key, cur, false); changed();
                    saveTimer.Stop(); saveTimer.Start();
                };
                sl.PreviewMouseWheel += (s, e) => { sl.Value = Math.Max(0, Math.Min(100, Math.Round(sl.Value) + (e.Delta > 0 ? 3 : -3))); e.Handled = true; };
                cell.Children.Add(lab); cell.Children.Add(sl);
                grid.Children.Add(cell);
            }
            sp.Children.Add(grid);
            if (!hasLv) sp.Children.Add(new TextBlock { Text = "No saved levels (only EQ). Click Use my current levels to save some.", Foreground = (Brush)FindResource("Muted"), FontSize = 11.5, Margin = new Thickness(0, 4, 0, 0) });

            // EQ
            var eqRow = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
            if (hasEq)
            {
                var fe = Btn("Forget EQ", "Go back to your normal headset EQ for " + name, () => { eng.ForgetGame(key, false, true); changed(); Fill(); });
                DockPanel.SetDock(fe, Dock.Right); eqRow.Children.Add(fe);
            }
            var eqText = hasEq
                ? "EQ: " + string.Join(",  ", geq.Values.Select(e => ShortName(e.Name) + " - " + (e.On ? (e.Param ? "parametric" : (e.Preset ?? "custom")) : "off")))
                : "EQ: your normal EQ" + (eng.S.EqPerGame ? " (change the EQ while it runs to give it its own)" : "");
            eqRow.Children.Add(new TextBlock { Text = eqText, Foreground = (Brush)FindResource("Muted"), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            sp.Children.Add(eqRow);
            card.Child = sp;
            return card;
        }
        static string ShortName(string n)
        {
            if (string.IsNullOrEmpty(n)) return "Device";
            int i = n.IndexOf(" ("); var inner = i > 0 && n.EndsWith(")") ? n.Substring(i + 2, n.Length - i - 3) : null;
            return inner != null && inner.Length < 30 ? inner : (n.Length > 30 ? n.Substring(0, 30) : n);
        }
    }

    // five equal columns
    public class UniformGrid5 : System.Windows.Controls.Primitives.UniformGrid { public UniformGrid5() { Columns = 5; } }
}
