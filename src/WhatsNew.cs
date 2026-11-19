// Hallow Mixer - "What's new": shown once after an update, with the versions you skipped.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HallowMixer
{
    public static class WhatsNew
    {
        // Newest first. One or two short lines per version.
        public static readonly Tuple<string, string[]>[] Notes = {
            Tuple.Create("3.18", new[] { "Settings > Sidetone: hear your own mic in your headset (about 25 ms behind, silent while muted)" }),
            Tuple.Create("3.17", new[] { "The Hallow accent: pick a colour in Hallow Toolkit and Hallow Mixer's buttons and highlights follow (Settings to turn off)", "If Hallow Mixer ever closes unexpectedly, a crash report is kept on this PC and you're told next time" }),
            Tuple.Create("3.16", new[] { "Mic chain (EQ window > your mic): noise gate, compressor and limiter - Light, Streamer or Loud room, with simple sliders", "Games window: a mic chain for each game, next to its mic preset" }),
            Tuple.Create("3.15", new[] { "One Hallow icon: with Hallow Hub 2.4 running, Hallow Mixer's menu is in Hub's tray icon and its own icon hides (Settings > Use the Hallow icon)" }),
            Tuple.Create("3.14", new[] { "Games window: pick a mic preset for each game - it goes on when the game starts, your own mic EQ comes back when it closes", "Hallow Overlay and Hallow Remote hear about mic, preset and profile changes right away" }),
            Tuple.Create("3.13", new[] { "A clear warning at the top of the mixer when your mic is muted in Windows, with one click to unmute", "If Hallow Mixer isn't set to start with Windows, it says so - one click turns it on" }),
            Tuple.Create("3.12", new[] { "A modern look: Font Awesome icons on every button, icon badges on each channel, rounder cards", "Header buttons are icons now - hover one to see what it does" }),
            Tuple.Create("3.11", new[] { "Settings > Keep my headset and mic: if Windows moves your sound or mic to something else (like a monitor waking up), it's moved straight back" }),
            Tuple.Create("3.10", new[] { "A new icon: three channel faders instead of the plain H" }),
            Tuple.Create("3.9", new[] { "This window - what's new after every update", "Switch profiles from your phone (Hallow Toolkit 4.9 dashboard)" }),
            Tuple.Create("3.8", new[] { "Mic strip > Set my level for me: talk for 5 seconds, your mic level is set for you" }),
            Tuple.Create("3.7", new[] { "Split headset (Chat strip): A50 / Arctis game and chat sides, with a voice EQ for Chat" }),
            Tuple.Create("3.6", new[] { "Profiles: whole mixer setups for different people or uses (Settings, tray, --profile)" }),
            Tuple.Create("3.5", new[] { "Quiet hours: a volume cap and quieter Windows sounds at night (Settings)" }),
            Tuple.Create("3.4", new[] { "Stream Deck and shortcuts: --preset, --toggle-mic, --channel game 60 (Settings > Shortcuts)" }),
            Tuple.Create("3.3", new[] { "A pop-up when you talk while your mic is muted" }),
            Tuple.Create("3.2", new[] { "Loud-sound guard: sudden blasts are turned down for a moment (Settings)" }),
            Tuple.Create("3.1", new[] { "Right-click an app > When I'm in another app: mute it or turn it down when you alt-tab" }),
            Tuple.Create("3.0", new[] { "EQ > Headphone space: crossfeed so headphones sound like speakers in a room" }),
            Tuple.Create("2.9", new[] { "Games (top bar): every game's saved levels and EQ" }),
            Tuple.Create("2.8", new[] { "Ctrl+Alt+1 to 9 = your presets, Ctrl+Alt+0 = next preset" }),
            Tuple.Create("2.7", new[] { "Hallow Toolkit can post what you're listening to and your mic status to Discord (opt-in)" }),
            Tuple.Create("2.6", new[] { "Smarter ducking: only real talking turns your game down" }),
            Tuple.Create("2.5", new[] { "Now playing: song or video with play/pause and skip" }),
        };

        static int Cmp(string a, string b) { Version x, y; return Version.TryParse(a, out x) && Version.TryParse(b, out y) ? x.CompareTo(y) : string.CompareOrdinal(a, b); }

        // The versions newer than `seen` (at most 6). Empty for a fresh install or no update.
        public static List<Tuple<string, string[]>> Since(string seen)
        {
            if (string.IsNullOrEmpty(seen)) return new List<Tuple<string, string[]>>();
            return Notes.Where(n => Cmp(n.Item1, seen) > 0 && Cmp(n.Item1, Program.Version) <= 0).Take(6).ToList();
        }
    }

    public class WhatsNewWindow : Window
    {
        static SolidColorBrush B(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        public WhatsNewWindow(List<Tuple<string, string[]>> items)
        {
            Title = "What's new in Hallow Mixer"; Width = 560; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
            Background = (Brush)FindResource("Bg"); Foreground = (Brush)FindResource("Text");
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12.5; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var sp = new StackPanel { Margin = new Thickness(26, 20, 26, 20) };
            sp.Children.Add(new TextBlock { Text = "What's new in Hallow Mixer " + Program.Version, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Accent.Brush, Margin = new Thickness(0, 0, 0, 12) });
            foreach (var v in items)
            {
                sp.Children.Add(new TextBlock { Text = v.Item1, FontWeight = FontWeights.Bold, Foreground = B("#A98BE0"), Margin = new Thickness(0, 6, 0, 3) });
                foreach (var line in v.Item2)
                {
                    var d = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
                    var dot = new TextBlock { Text = "\u2022", Foreground = Accent.Brush, Width = 16, FontWeight = FontWeights.Bold }; DockPanel.SetDock(dot, Dock.Left);
                    d.Children.Add(dot); d.Children.Add(new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap });
                    sp.Children.Add(d);
                }
            }
            var foot = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var all = new Button { Content = "All release notes", Style = (Style)FindResource("Btn"), Margin = new Thickness(0, 0, 8, 0) };
            all.Click += (s, e) => { try { Process.Start(new ProcessStartInfo("https://github.com/Dom428/hallow-mixer/releases") { UseShellExecute = true }); } catch { } };
            var ok = new Button { Content = "Got it", Style = (Style)FindResource("Accent"), MinWidth = 90 };
            ok.Click += (s, e) => Close();
            foot.Children.Add(all); foot.Children.Add(ok); sp.Children.Add(foot);
            Content = sp;
        }
    }
}
