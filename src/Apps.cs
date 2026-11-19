// Hallow Mixer - v3.22 app rules: every app Hallow Mixer remembers (running or not) with its channel, start level, output,
// background behaviour and mute in one place - and apps you add by name before they ever play a sound.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HallowMixer
{
    public class AppsWindow : Window
    {
        readonly Engine eng; readonly Action changed;
        readonly StackPanel list = new StackPanel();
        readonly HashSet<string> added = new HashSet<string>();   // added here, no choice saved yet (shown until the window closes)
        readonly TextBox addBox; readonly TextBlock addNote;
        static readonly Dictionary<string, string> Hex = new Dictionary<string, string> {
            { "game", "#F07820" }, { "chat", "#A98BE0" }, { "music", "#6ED28C" }, { "media", "#5AB4F0" }, { "other", "#8F88A0" } };
        static SolidColorBrush B(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        const string Remember = "Where I left it";
        public static readonly int[] StartChoices = { 0, 10, 20, 25, 30, 40, 50, 60, 70, 75, 80, 90, 100 };

        public AppsWindow(Engine engine, Action onChanged)
        {
            eng = engine; changed = onChanged;
            Title = "Hallow Mixer - apps"; Width = 940; Height = 620; MinWidth = 760; MinHeight = 360;
            Background = (Brush)FindResource("Bg"); Foreground = (Brush)FindResource("Text");
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12.5;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var dp = new DockPanel { Margin = new Thickness(22, 16, 22, 16) };
            var head = new StackPanel(); DockPanel.SetDock(head, Dock.Top);
            head.Children.Add(new TextBlock { Text = "App rules", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Accent.Brush });
            head.Children.Add(new TextBlock { Text = "Where each app goes and how loud it starts - also for apps that aren't running. \"Starts at\" puts the app back to that level every time it starts, whatever you changed it to last time.",
                Foreground = (Brush)FindResource("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 10) });
            var addRow = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            var addBtn = new Button { Content = "Add app", Style = (Style)FindResource("Btn"), Margin = new Thickness(8, 0, 0, 0), ToolTip = "Add an app by its program name, before it plays anything" };
            System.Windows.Automation.AutomationProperties.SetName(addBtn, "Add app");
            DockPanel.SetDock(addBtn, Dock.Left);
            addBox = new TextBox { Width = 240, Background = (Brush)FindResource("PanelHi"), Foreground = (Brush)FindResource("Text"), BorderBrush = (Brush)FindResource("Line"), Padding = new Thickness(6, 4, 6, 4), CaretBrush = Brushes.White,
                ToolTip = "The program's name, e.g. spotify or Spotify.exe" };
            System.Windows.Automation.AutomationProperties.SetName(addBox, "App to add");
            DockPanel.SetDock(addBox, Dock.Left);
            addNote = new TextBlock { Text = "Program name, e.g. spotify, discord or vlc.exe", Foreground = (Brush)FindResource("Muted"), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            addBtn.Click += (s, e) => AddTyped();
            addBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) { AddTyped(); e.Handled = true; } };
            addRow.Children.Add(addBox); addRow.Children.Add(addBtn); addRow.Children.Add(addNote);
            head.Children.Add(addRow);
            dp.Children.Add(head);
            dp.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            Content = dp;
            Fill();
        }

        // returns the key, or null with a reason in addNote
        public string AddApp(string typed)
        {
            var key = Engine.CleanAppKey(typed);
            if (key == null) { addNote.Text = "That doesn't look like a program name - type it like spotify or Spotify.exe."; return null; }
            if (eng.RememberedApps().Contains(key)) addNote.Text = eng.AppName(key) + " is already in the list.";
            else { added.Add(key); addNote.Text = "Added " + key + " - pick its channel or start level below."; }
            Fill(); return key;
        }
        void AddTyped() { if (AddApp(addBox.Text) != null) addBox.Text = ""; }

        public void Fill()
        {
            list.Children.Clear();
            var keys = eng.RememberedApps().Union(added).Distinct()
                .OrderByDescending(k => eng.AppRunning(k)).ThenBy(k => eng.AppName(k), StringComparer.OrdinalIgnoreCase).ToList();
            if (keys.Count == 0)
            {
                list.Children.Add(new TextBlock { Text = "No apps yet. Apps show up here when they play sound, or add one by name above.",
                    Foreground = (Brush)FindResource("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 20, 0, 0) });
                return;
            }
            List<AudioDevice> outs; try { outs = CoreAudio.Devices(0); } catch { outs = new List<AudioDevice>(); }
            foreach (var k in keys) list.Children.Add(Row(k, outs));
        }

        ComboBox Combo(double width, string name, string tip)
        {
            var c = new ComboBox { Style = (Style)FindResource("Combo"), Width = width, Margin = new Thickness(0, 0, 10, 0), ToolTip = tip };
            System.Windows.Automation.AutomationProperties.SetName(c, name);
            return c;
        }
        static StackPanel Labelled(string label, Control c)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            sp.Children.Add(new TextBlock { Text = label, FontSize = 11, Opacity = 0.7, Margin = new Thickness(0, 0, 0, 3) });
            sp.Children.Add(c); return sp;
        }
        class Item { public string Text; public object Value; public override string ToString() { return Text; } }
        static void Select(ComboBox c, object v) { foreach (Item i in c.Items) if (Equals(i.Value, v)) { c.SelectedItem = i; return; } }

        UIElement Row(string key, List<AudioDevice> outs)
        {
            var name = eng.AppName(key); bool running = eng.AppRunning(key);
            var card = new Border { Background = (Brush)FindResource("Panel"), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 10, 16, 12), Margin = new Thickness(0, 0, 0, 8) };
            System.Windows.Automation.AutomationProperties.SetName(card, name);
            var sp = new StackPanel();
            var top = new DockPanel();
            var forget = new Button { Content = "Forget", Style = (Style)FindResource("Btn"), ToolTip = "Forget everything saved for " + name + " - it goes back to automatic" };
            System.Windows.Automation.AutomationProperties.SetName(forget, "Forget " + name);
            forget.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Forget " + name + "'s channel, levels, output and rules?", "Hallow Mixer", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                eng.ForgetApp(key); added.Remove(key); changed(); Fill();
            };
            DockPanel.SetDock(forget, Dock.Right); top.Children.Add(forget);
            var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            title.Children.Add(new TextBlock { Text = name, FontSize = 14.5, FontWeight = FontWeights.Bold });
            if (!string.Equals(name, key, StringComparison.OrdinalIgnoreCase)) title.Children.Add(new TextBlock { Text = key + ".exe", Foreground = (Brush)FindResource("Muted"), FontSize = 11, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            if (running) title.Children.Add(new Border { Background = Accent.Brush, CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = "PLAYING NOW", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Accent.Text } });
            top.Children.Add(title);
            sp.Children.Add(top);

            var wrap = new WrapPanel();
            // channel
            string auto = eng.AutoChannelOf(key), cur; bool picked = eng.S.AppChannel.TryGetValue(key, out cur);
            var ch = Combo(185, name + " channel", "Which channel " + name + " plays in");
            ch.Items.Add(new Item { Text = "Automatic (" + Engine.ChannelNames[auto] + ")", Value = "" });
            foreach (var id in Engine.ChannelIds) ch.Items.Add(new Item { Text = Engine.ChannelNames[id], Value = id });
            Select(ch, picked ? cur : "");
            var chLabel = Labelled("Channel", ch);
            ((TextBlock)chLabel.Children[0]).Foreground = B(Hex[picked && Hex.ContainsKey(cur) ? cur : auto]);
            ch.SelectionChanged += (s, e) =>
            {
                var it = ch.SelectedItem as Item; if (it == null) return;
                var v = (string)it.Value; eng.SetAppChannel(key, v == "" ? null : v);
                ((TextBlock)chLabel.Children[0]).Foreground = B(Hex[v == "" ? auto : v]); changed();
            };
            wrap.Children.Add(chLabel);

            // starts at
            int? start = eng.StartLevel(key);
            var st = Combo(170, name + " starts at", "\"" + Remember + "\": " + name + " keeps the level you last gave it. A percentage: it's put back to that every time it starts.");
            st.Items.Add(new Item { Text = Remember + " (" + eng.AppVolume(key) + "%)", Value = -1 });
            var choices = StartChoices.ToList(); if (start != null && !choices.Contains(start.Value)) { choices.Add(start.Value); choices.Sort(); }
            foreach (var v in choices) st.Items.Add(new Item { Text = "Always " + v + "%", Value = v });
            Select(st, start ?? -1);
            st.SelectionChanged += (s, e) =>
            {
                var it = st.SelectedItem as Item; if (it == null) return;
                int v = (int)it.Value;
                eng.SetStartLevel(key, v < 0 ? (int?)null : v, name);
                if (v >= 0 && running) { eng.ApplyStart(key, v); eng.Apply(); }   // running now: take the level straight away too
                changed();
            };
            wrap.Children.Add(Labelled("Starts at", st));

            // output
            string outId; bool own = eng.S.AppOutput.TryGetValue(key, out outId);
            var oc = Combo(220, name + " output", "Where " + name + " plays - its channel's output, or one of its own");
            oc.IsEnabled = CoreAudio.CanRoutePerApp;
            oc.Items.Add(new Item { Text = "Same as its channel", Value = "" });
            foreach (var d in outs) oc.Items.Add(new Item { Text = d.Name, Value = d.Id });
            if (own && !outs.Any(d => d.Id == outId)) oc.Items.Add(new Item { Text = "(a device that's not plugged in)", Value = outId });
            Select(oc, own ? outId : "");
            oc.SelectionChanged += (s, e) =>
            {
                var it = oc.SelectedItem as Item; if (it == null) return;
                var v = (string)it.Value; eng.SetAppOutput(key, v == "" ? null : v); changed();
            };
            wrap.Children.Add(Labelled("Plays on", oc));

            // in the background
            int bg; bool hasBg = eng.S.BgLevel.TryGetValue(key, out bg);
            var bc = Combo(150, name + " in the background", "What " + name + " does while you're in another app");
            bc.Items.Add(new Item { Text = "Keep playing", Value = -1 }); bc.Items.Add(new Item { Text = "Turn down to 20%", Value = 20 }); bc.Items.Add(new Item { Text = "Mute", Value = 0 });
            if (hasBg && bg != 20 && bg != 0) bc.Items.Add(new Item { Text = "Turn down to " + bg + "%", Value = bg });
            Select(bc, hasBg ? bg : -1);
            bc.SelectionChanged += (s, e) =>
            {
                var it = bc.SelectedItem as Item; if (it == null) return;
                eng.SetBgLevel(key, (int)it.Value); changed();
            };
            wrap.Children.Add(Labelled("When I'm in another app", bc));

            // muted
            var mute = new CheckBox { Content = "Muted", Style = (Style)FindResource("Switch"), IsChecked = eng.AppMuted(key), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(4, 0, 0, 6) };
            System.Windows.Automation.AutomationProperties.SetName(mute, name + " muted");
            mute.Checked += (s, e) => { eng.SetAppMuted(key, true); changed(); };
            mute.Unchecked += (s, e) => { eng.SetAppMuted(key, false); changed(); };
            var mwrap = new Grid { Margin = new Thickness(0, 8, 0, 0) }; mwrap.Children.Add(mute);
            wrap.Children.Add(mwrap);
            sp.Children.Add(wrap);
            card.Child = sp;
            return card;
        }
    }
}
