// Hallow Mixer - first-run setup: "With Sonar / Replacing Sonar / Just a mixer". Sets channels, outputs and mic for you.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HallowMixer
{
    // One thing setup will do: shown to you first, then done.
    public class SetupStep { public string Text; public Action Run; }

    public static class Setup
    {
        public static bool IsSonar(AudioDevice d) { return d != null && d.Name.IndexOf("SteelSeries Sonar", StringComparison.OrdinalIgnoreCase) >= 0; }
        public static AudioDevice SonarDev(List<AudioDevice> outs, string part)
        {
            return outs.FirstOrDefault(d => IsSonar(d) && d.Name.IndexOf("- " + part, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        public static bool HasSonar(List<AudioDevice> outs) { return SonarDev(outs, "Gaming") != null; }

        static bool Has(AudioDevice d, params string[] words) { return words.Any(w => d.Name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0); }

        // Real (not Sonar) devices, best guess first.
        static int HeadsetScore(AudioDevice d)
        {
            if (IsSonar(d)) return -100;
            int s = 0;
            if (Has(d, "Headphones", "Headset", "A50", "A40", "Arctis", "HyperX", "Cloud", "Astro", "BlackShark", "Kraken", "Corsair", "Logitech G", "Nova", "Virtuoso", "HS80", "Barracuda")) s += 10;
            if (Has(d, "Headphones")) s += 4;
            if (Has(d, "Voice", "Hands-Free", "Chat", "Earphone")) s -= 8;     // the headset's call/chat endpoint, not the main one
            if (Has(d, "Digital Output", "SPDIF", "S/PDIF", "NVIDIA", "AMD High Definition", "Intel(R) Display", "LCD", "Monitor", "HDMI", "DisplayPort")) s -= 6;
            if (d.IsDefault) s += 1;
            return s;
        }
        static int SpeakerScore(AudioDevice d)
        {
            if (IsSonar(d)) return -100;
            int s = 0;
            if (Has(d, "Speakers", "Speaker")) s += 10;
            if (Has(d, "Realtek")) s += 3;
            if (Has(d, "LCD", "Monitor", "HDMI", "DisplayPort", "NVIDIA", "AMD High Definition")) s += 1;
            if (Has(d, "Digital Output", "SPDIF", "S/PDIF")) s -= 2;
            if (HeadsetScore(d) >= 10 || Has(d, "Voice", "Hands-Free", "Earphone")) s -= 20;
            return s;
        }
        static int MicScore(AudioDevice d)
        {
            if (IsSonar(d)) return -100;
            int s = 0;
            if (Has(d, "QuadCast", "Yeti", "Wave", "SoloCast", "Seiren", "Shure", "Rode", "RØDE", "Elgato", "Fifine", "Razer", "AT2020", "Samson", "Focusrite", "Scarlett", "GoXLR")) s += 12;
            if (Has(d, "Microphone", "Mic")) s += 5;
            if (Has(d, "Headset")) s += 2;
            if (Has(d, "Line", "Stream", "Stereo Mix", "What U Hear", "Wave Out", "Loopback", "CABLE", "VoiceMeeter", "Virtual")) s -= 15;
            if (Has(d, "Webcam", "Camera", "C920", "Brio")) s -= 3;
            return s;
        }
        static AudioDevice Best(List<AudioDevice> list, Func<AudioDevice, int> score, int min)
        {
            var b = list.OrderByDescending(score).FirstOrDefault();
            return b != null && score(b) >= min ? b : null;
        }
        public static AudioDevice GuessHeadset(List<AudioDevice> outs) { return Best(outs, HeadsetScore, 0); }
        public static AudioDevice GuessSpeakers(List<AudioDevice> outs, AudioDevice headset)
        {
            return Best(outs.Where(d => headset == null || d.Id != headset.Id).ToList(), SpeakerScore, 0);
        }
        public static AudioDevice GuessMic(List<AudioDevice> ins) { return Best(ins, MicScore, 0); }

        // v3.7: a headset that shows up as two outputs, "Headphones (A50 Game)" + "Headset Earphone (A50 Voice)" or
        // "(Arctis Nova 7 Game)" + "(Arctis Nova 7 Chat)". Returns the game and chat outputs, or null.
        static string Inner(string n) { int i = n.LastIndexOf('('); return i >= 0 && n.EndsWith(")") ? n.Substring(i + 1, n.Length - i - 2).Trim() : null; }
        public static Tuple<AudioDevice, AudioDevice> FindSplit(List<AudioDevice> outs)
        {
            foreach (var g in outs.Where(d => !IsSonar(d)))
            {
                var gi = Inner(g.Name); if (gi == null || !gi.EndsWith(" Game", StringComparison.OrdinalIgnoreCase)) continue;
                var model = gi.Substring(0, gi.Length - 5);
                var c = outs.FirstOrDefault(d => { var ci = Inner(d.Name); return ci != null && (string.Equals(ci, model + " Chat", StringComparison.OrdinalIgnoreCase) || string.Equals(ci, model + " Voice", StringComparison.OrdinalIgnoreCase)); });
                if (c != null) return Tuple.Create(g, c);
            }
            return null;
        }

        // What setup will do. mode: "sonar", "replace" or "mixer". Devices may be null (= leave it).
        public static List<SetupStep> Plan(Engine eng, string mode, List<AudioDevice> outs, AudioDevice headset, AudioDevice fallback, AudioDevice mic)
        {
            var steps = new List<SetupStep>();
            var S = eng.S;
            if (mode == "sonar")
            {
                var map = new[] { new[] { "game", "Gaming" }, new[] { "chat", "Chat" }, new[] { "music", "Media" }, new[] { "media", "Media" }, new[] { "other", "Aux" } };
                foreach (var m in map)
                {
                    var d = SonarDev(outs, m[1]); if (d == null) continue;
                    var ch = m[0]; var id = d.Id;
                    steps.Add(new SetupStep { Text = Engine.ChannelNames[ch] + " apps play into Sonar's " + m[1] + " channel", Run = () => { eng.Chan(ch).OutputId = id; if (ch == "chat") S.ChatClean = false; } });
                }
                steps.Add(new SetupStep { Text = "Headset-first switching off (Sonar picks your headset)", Run = () => S.AutoSwitch = false });
                steps.Add(new SetupStep { Text = "EQ, mic and headset output stay in Sonar - Hallow Mixer handles levels, apps, presets and hotkeys", Run = () => { } });
            }
            else if (mode == "replace")
            {
                steps.Add(new SetupStep { Text = "Every channel plays on your default output (no Sonar devices)", Run = () => { foreach (var c in S.Channels) c.OutputId = ""; S.AppOutput.Clear(); S.ChatClean = false; } });
                steps.Add(new SetupStep { Text = "Apps Sonar had sent to its own devices go back to your default output", Run = () => eng.ForgetRoutes() });
                if (headset != null)
                {
                    var hid = headset.Id;
                    steps.Add(new SetupStep { Text = "Default output: " + headset.Name, Run = () => CoreAudio.SetDefault(hid) });
                }
                if (headset != null && fallback != null)
                {
                    var hid = headset.Id; var fid = fallback.Id;
                    steps.Add(new SetupStep { Text = "Headset first: " + headset.Name + " whenever it's on, otherwise " + fallback.Name, Run = () => { S.AutoSwitch = true; S.PrimaryOut = hid; S.FallbackOut = fid; } });
                }
                if (mic != null)
                {
                    var mid = mic.Id;
                    steps.Add(new SetupStep { Text = "Mic for games and Discord: " + mic.Name, Run = () => CoreAudio.SetDefault(mid) });
                }
            }
            else
            {
                if (S.Channels.Any(c => !string.IsNullOrEmpty(c.OutputId)))
                    steps.Add(new SetupStep { Text = "Every channel plays on your default output", Run = () => { foreach (var c in S.Channels) c.OutputId = ""; S.ChatClean = false; } });
                steps.Add(new SetupStep { Text = "Your devices stay as they are - Hallow Mixer sorts apps into channels and handles levels", Run = () => { } });
            }
            steps.Add(new SetupStep { Text = "Setup remembered (run it again any time from Settings)", Run = () => { S.SetupMode = mode; S.SetupDone = true; } });
            return steps;
        }
    }

    public class SetupWindow : Window
    {
        readonly Engine eng;
        readonly Action<string, bool, bool> done;   // mode, discord-ready mic, start with Windows
        readonly Grid page = new Grid();
        List<AudioDevice> outs, ins;
        string mode;
        ComboBox headsetBox, fallbackBox, micBox;
        CheckBox readyBox, startBox;

        static SolidColorBrush B(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }

        public SetupWindow(Engine engine, Action<string, bool, bool> onDone)
        {
            eng = engine; done = onDone;
            Title = "Hallow Mixer setup"; Width = 720; Height = 560; ResizeMode = ResizeMode.NoResize;
            Background = Look.Bg; Foreground = Look.Text;
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12.5;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Content = new Border { Padding = new Thickness(28, 22, 28, 22), Child = page };
            outs = CoreAudio.Devices(CoreAudio.Render); ins = CoreAudio.Devices(CoreAudio.Capture);
            ShowChoice();
        }

        StackPanel NewPage(string title, string sub)
        {
            page.Children.Clear();
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Accent.Ink });
            if (sub != null) sp.Children.Add(new TextBlock { Text = sub, Foreground = Look.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 16) });
            page.Children.Add(sp);
            return sp;
        }
        Border Card(string title, string text, bool enabled, Action click)
        {
            var b = new Border { Background = Look.Panel, CornerRadius = new CornerRadius(8), Padding = new Thickness(18, 14, 18, 14), Margin = new Thickness(0, 0, 0, 10),
                BorderThickness = new Thickness(1), BorderBrush = Look.Line, Cursor = enabled ? Cursors.Hand : Cursors.Arrow, Opacity = enabled ? 1 : 0.45 };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.Bold });
            sp.Children.Add(new TextBlock { Text = text, Foreground = Look.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            b.Child = sp;
            if (enabled)
            {
                b.MouseEnter += (s, e) => b.BorderBrush = Accent.Ink;
                b.MouseLeave += (s, e) => b.BorderBrush = Look.Line;
                b.MouseLeftButtonUp += (s, e) => click();
            }
            AutomationName(b, title);
            return b;
        }
        static void AutomationName(FrameworkElement e, string n) { System.Windows.Automation.AutomationProperties.SetName(e, n); }
        Button Btn(string text, bool accent, Action click)
        {
            var b = new Button { Content = text, Style = (Style)FindResource(accent ? "Accent" : "Btn"), Margin = new Thickness(8, 0, 0, 0), MinWidth = 90 };
            b.Click += (s, e) => click(); return b;
        }
        void Footer(params Button[] buttons)
        {
            var f = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
            foreach (var b in buttons) f.Children.Add(b);
            page.Children.Add(f);
        }

        void ShowChoice()
        {
            bool sonar = Setup.HasSonar(outs);
            var sp = NewPage("Welcome to Hallow Mixer", "How do you want to use it? You can change this later in Settings > Run setup again.");
            sp.Children.Add(Card("With Sonar", sonar
                ? "Keep SteelSeries Sonar for EQ and your headset. Hallow Mixer sends each channel into the matching Sonar channel and adds app sorting, presets, per-game levels and hotkeys."
                : "SteelSeries Sonar isn't on this PC (its virtual devices weren't found).", sonar, () => { mode = "sonar"; ShowOptions(); }));
            sp.Children.Add(Card("Replacing Sonar", "Hallow Mixer does the job on its own: your headset and real mic become the defaults, apps Sonar moved come back, and you can use Hallow's EQ (needs the free Equalizer APO).",
                true, () => { mode = "replace"; ShowOptions(); }));
            sp.Children.Add(Card("Just a mixer", "Leave my devices alone. Sort apps into Game, Chat, Music, Media and System, with levels, presets and hotkeys.",
                true, () => { mode = "mixer"; ShowOptions(); }));
            Footer(Btn("Skip for now", false, () => { eng.S.SetupDone = true; eng.S.Save(); Close(); }));
        }

        ComboBox Pick(StackPanel sp, string label, List<AudioDevice> list, AudioDevice guess, bool allowNone)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var l = new TextBlock { Text = label, Width = 150, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(l, Dock.Left); row.Children.Add(l);
            var items = new List<AudioDevice>();
            if (allowNone) items.Add(new AudioDevice { Id = "", Name = "None" });
            items.AddRange(list.Where(d => !Setup.IsSonar(d)));
            var c = new ComboBox { Style = (Style)FindResource("Combo"), ItemsSource = items };
            c.SelectedItem = guess == null ? items.FirstOrDefault() : items.FirstOrDefault(d => d.Id == guess.Id);
            AutomationName(c, label);
            row.Children.Add(c); sp.Children.Add(row);
            return c;
        }
        CheckBox Switch(StackPanel sp, string text, bool on)
        {
            var c = new CheckBox { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, IsChecked = on, Style = (Style)FindResource("Switch") };
            AutomationName(c, text); sp.Children.Add(c); return c;
        }

        void ShowOptions()
        {
            headsetBox = fallbackBox = micBox = null;
            StackPanel sp;
            if (mode == "replace")
            {
                sp = NewPage("Replacing Sonar", "Check these guesses - pick the right one if they're wrong.");
                var h = Setup.GuessHeadset(outs);
                headsetBox = Pick(sp, "Headset", outs, h, false);
                fallbackBox = Pick(sp, "When it's off, use", outs, Setup.GuessSpeakers(outs, h), true);
                micBox = Pick(sp, "Mic", ins, Setup.GuessMic(ins), false);
                sp.Children.Add(new TextBlock { Height = 6 });
                readyBox = Switch(sp, "Make my mic Discord-ready (good level that stays put, voice EQ, rumble cut)", true);
                startBox = Switch(sp, "Start Hallow Mixer with Windows (in the tray)", true);
                sp.Children.Add(new TextBlock { Text = "Last step is yours: in SteelSeries GG, turn Sonar off so it stops adding its own devices. Hallow Mixer doesn't uninstall or change GG.",
                    Foreground = Look.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
            }
            else
            {
                sp = NewPage(mode == "sonar" ? "With Sonar" : "Just a mixer", null);
                sp.Margin = new Thickness(0);
                readyBox = null;
                startBox = Switch(sp, "Start Hallow Mixer with Windows (in the tray)", true);
            }
            Footer(Btn("Back", false, ShowChoice), Btn("Next", true, ShowReview));
        }

        AudioDevice Sel(ComboBox c) { var d = c == null ? null : c.SelectedItem as AudioDevice; return d == null || d.Id == "" ? null : d; }

        void ShowReview()
        {
            var steps = Setup.Plan(eng, mode, outs, Sel(headsetBox), Sel(fallbackBox), Sel(micBox));
            bool ready = readyBox != null && readyBox.IsChecked == true, start = startBox != null && startBox.IsChecked == true;
            var sp = NewPage("Here's what will change", "Nothing changes until you click Set it up.");
            foreach (var st in steps.Take(steps.Count - 1)) sp.Children.Add(Line(st.Text));
            if (ready) sp.Children.Add(Line("Mic: Discord-ready (unmuted, kept at a good level, voice EQ)"));
            if (start) sp.Children.Add(Line("Starts with Windows, in the tray"));
            sp.Children.Add(Line(steps.Last().Text));
            Footer(Btn("Back", false, ShowOptions), Btn("Set it up", true, () =>
            {
                var failed = new List<string>();
                foreach (var st in steps) { try { st.Run(); } catch { failed.Add(st.Text); } }
                eng.S.Save(); eng.Apply();
                done(mode, ready, start);
                ShowDone(failed);
            }));
        }
        static UIElement Line(string text)
        {
            var d = new DockPanel { Margin = new Thickness(0, 0, 0, 7) };
            var dot = new TextBlock { Text = "•", Foreground = Accent.Ink, Width = 16, FontWeight = FontWeights.Bold }; DockPanel.SetDock(dot, Dock.Left);
            d.Children.Add(dot); d.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }); return d;
        }

        void ShowDone(List<string> failed)
        {
            var sp = NewPage("You're set", failed.Count == 0 ? "Hallow Mixer is ready. Some apps move to their new output only after they restart." : "Mostly done - these didn't work:");
            foreach (var f in failed) sp.Children.Add(Line(f));
            sp.Children.Add(Line("Right-click any app to move it to another channel"));
            sp.Children.Add(Line("Ctrl+Alt+PageUp / PageDown = game/chat balance,  Ctrl+Alt+M = mute mic"));
            sp.Children.Add(Line("EQ, Stream (OBS) and Mini are in the top bar"));
            Footer(Btn("Open the mixer", true, Close));
        }
    }
}
