// Hallow Mixer - "now playing": the song or video Windows knows about (the same info as the volume pop-up / media keys),
// with play/pause and skip. Read through Windows' media session API; nothing is sent anywhere.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Windows.Foundation;
using Windows.Media.Control;

namespace HallowMixer
{
    public class MediaInfo
    {
        public string App, Key, Title, Artist;
        public bool Playing, CanPrev, CanNext, CanPlayPause;
        public string Line { get { return string.IsNullOrEmpty(Artist) ? Title : Title + " - " + Artist; } }
    }

    public static class NowPlaying
    {
        static GlobalSystemMediaTransportControlsSessionManager mgr;
        static int busy;
        public static volatile MediaInfo Current;

        static T Wait<T>(IAsyncOperation<T> op)
        {
            var end = DateTime.UtcNow.AddMilliseconds(2000);
            while (op.Status == AsyncStatus.Started)
            {
                if (DateTime.UtcNow > end) { try { op.Cancel(); } catch { } throw new TimeoutException(); }
                Thread.Sleep(10);
            }
            if (op.Status != AsyncStatus.Completed) throw new InvalidOperationException("media session: " + op.Status);
            return op.GetResults();
        }

        static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "chrome", "Chrome" }, { "msedge", "Edge" }, { "308046B0AF4A39CB", "Firefox" }, { "firefox", "Firefox" }, { "spotify", "Spotify" },
            { "brave", "Brave" }, { "opera", "Opera" }, { "opera_gx", "Opera GX" }, { "vlc", "VLC" }, { "Microsoft.ZuneMusic", "Media Player" }, { "AppleMusic", "Apple Music" } };

        // "Spotify.exe", "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "MSEdge", "308046B0AF4A39CB" -> a key like the mixer's app keys, and a name
        public static string KeyFor(string aumid)
        {
            if (string.IsNullOrEmpty(aumid)) return "";
            var s = aumid;
            int bang = s.IndexOf('!'); if (bang >= 0) s = s.Substring(bang + 1);
            if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 4);
            int slash = s.LastIndexOfAny(new[] { '\\', '/' }); if (slash >= 0) s = s.Substring(slash + 1);
            return s.ToLowerInvariant();
        }
        public static string NameFor(string aumid)
        {
            if (string.IsNullOrEmpty(aumid)) return "";
            foreach (var kv in Names) if (aumid.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0) return kv.Value;
            var k = KeyFor(aumid); if (k.Length == 0) return aumid;
            return char.ToUpperInvariant(k[0]) + k.Substring(1);
        }

        static GlobalSystemMediaTransportControlsSession Pick()
        {
            if (mgr == null) mgr = Wait(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
            var all = mgr.GetSessions().ToList();
            // something actually playing wins; otherwise the one Windows calls current (e.g. paused music)
            var playing = all.FirstOrDefault(x => { try { return x.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing; } catch { return false; } });
            return playing ?? mgr.GetCurrentSession() ?? all.FirstOrDefault();
        }

        static MediaInfo Read()
        {
            var s = Pick(); if (s == null) return null;
            var p = Wait(s.TryGetMediaPropertiesAsync());
            if (p == null || string.IsNullOrWhiteSpace(p.Title)) return null;
            var pb = s.GetPlaybackInfo(); var c = pb.Controls;
            return new MediaInfo
            {
                App = NameFor(s.SourceAppUserModelId), Key = KeyFor(s.SourceAppUserModelId),
                Title = p.Title.Trim(), Artist = (p.Artist ?? "").Trim(),
                Playing = pb.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                CanPrev = c.IsPreviousEnabled, CanNext = c.IsNextEnabled, CanPlayPause = c.IsPlayPauseToggleEnabled || c.IsPlayEnabled || c.IsPauseEnabled
            };
        }

        // Reads on a background thread (never blocks the window); done gets the result, null = nothing playing.
        public static void Poll(Action<MediaInfo> done)
        {
            if (Interlocked.Exchange(ref busy, 1) == 1) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                MediaInfo r = null;
                try { r = Read(); } catch { mgr = null; }
                Current = r; Interlocked.Exchange(ref busy, 0);
                if (done != null) done(r);
            });
        }
        // what: "playpause", "next" or "prev"
        public static void Command(string what, Action after)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var s = Pick();
                    if (s != null)
                    {
                        if (what == "next") Wait(s.TrySkipNextAsync());
                        else if (what == "prev") Wait(s.TrySkipPreviousAsync());
                        else Wait(s.TryTogglePlayPauseAsync());
                    }
                }
                catch { }
                Thread.Sleep(350);
                if (after != null) after();
            });
        }
    }

    // The "now playing" strip (one in the mixer, one in the Mini mixer).
    public class NowBar : Border
    {
        readonly TextBlock title, sub; readonly Button prev, play, next; readonly Border dot;
        public Action<string> Command;
        static readonly FontFamily Icons = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");

        Button IconBtn(string glyph, string tip, string cmd)
        {
            var b = new Button { Content = glyph, FontFamily = Icons, FontSize = 12, Style = (Style)FindResource("Btn"), Padding = new Thickness(9, 4, 9, 4), Margin = new Thickness(4, 0, 0, 0), ToolTip = tip };
            System.Windows.Automation.AutomationProperties.SetName(b, tip);
            b.Click += (s, e) => { if (Command != null) Command(cmd); };
            return b;
        }

        public NowBar(bool compact)
        {
            Background = (Brush)FindResource("Panel"); CornerRadius = new CornerRadius(8);
            Padding = compact ? new Thickness(10, 6, 8, 6) : new Thickness(16, 8, 12, 8);
            Visibility = Visibility.Collapsed;
            var dp = new DockPanel();
            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            prev = IconBtn("", "Previous", "prev"); play = IconBtn("", "Play / pause", "playpause"); next = IconBtn("", "Next", "next");
            btns.Children.Add(prev); btns.Children.Add(play); btns.Children.Add(next);
            DockPanel.SetDock(btns, Dock.Right); dp.Children.Add(btns);
            dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(dot, Dock.Left); dp.Children.Add(dot);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            title = new TextBlock { FontWeight = FontWeights.SemiBold, FontSize = compact ? 11.5 : 12.5, TextTrimming = TextTrimming.CharacterEllipsis };
            sub = new TextBlock { Foreground = (Brush)FindResource("Muted"), FontSize = compact ? 10.5 : 11, TextTrimming = TextTrimming.CharacterEllipsis };
            text.Children.Add(title); text.Children.Add(sub);
            dp.Children.Add(text);
            Child = dp;
        }

        public void Show(MediaInfo m, Color channelColor, string channelName)
        {
            if (m == null) { Visibility = Visibility.Collapsed; return; }
            Visibility = Visibility.Visible;
            title.Text = m.Title; title.ToolTip = m.Line;
            sub.Text = (string.IsNullOrEmpty(m.Artist) ? "" : m.Artist + "  ·  ") + m.App + (channelName != null ? " in " + channelName : "") + (m.Playing ? "" : "  (paused)");
            dot.Background = new SolidColorBrush(channelColor); dot.Opacity = m.Playing ? 1 : 0.35;
            play.Content = m.Playing ? "" : "";
            prev.IsEnabled = m.CanPrev; next.IsEnabled = m.CanNext; play.IsEnabled = m.CanPlayPause;
        }
    }
}
