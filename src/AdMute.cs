// Hallow Mixer 3.27 - mute Spotify ads. Reads Spotify's own Windows media session (the same info as the volume pop-up) and the
// title of Spotify's window; while an ad plays, only Spotify's sound streams are muted (or lowered) - the mixer's per-app
// volume, the same as clicking Spotify's icon. Spotify itself is never changed: no files, no network, no account.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Windows.Media.Control;

namespace HallowMixer
{
    // What Spotify shows right now. Made by SpotifySource (live) or by hand (tests).
    public class AdSignal
    {
        public bool Session;    // Spotify has a Windows media session
        public string Title, Artist, Album;
        public int Type = -1;   // Windows' playback type: -1 not given, 0 unknown, 1 music, 2 video, 3 image
        public bool Playing;    // the media session says playing
        public string Window;   // Spotify's main window title (null = Spotify isn't running, "" = no titled window)
        public bool AudioActive;   // one of Spotify's sound streams is playing (the mixer's own view)
    }

    public interface IAdSource { AdSignal Read(); }

    // The rules, kept apart so they can be tested without Spotify.
    public static class AdRules
    {
        static bool Eq(string a, string b) { return string.Equals((a ?? "").Trim(), b, StringComparison.OrdinalIgnoreCase); }

        // The media session's opinion: true = an ad, false = a real track, null = can't tell.
        public static bool? FromSession(AdSignal s)
        {
            if (s == null || !s.Session) return null;
            var title = (s.Title ?? "").Trim(); var artist = (s.Artist ?? "").Trim();
            if (title.Length == 0 && artist.Length == 0) return null;          // nothing published yet (loading)
            if (s.Type == 2 || s.Type == 3) return true;                        // video / image: not music
            bool noArtist = artist.Length == 0 || Eq(artist, "Spotify") || Eq(artist, "Advertisement");
            if (noArtist) return true;                                          // "Advertisement" / "Spotify" / a brand with no artist
            return false;                                                       // a real artist and a normal track - even one titled "Advertisement"
        }

        // The window's opinion: Spotify shows "Artist - Song" while music plays, "Advertisement" or "Spotify" during ads,
        // "Spotify Free" / "Spotify Premium" when paused.
        public static bool? FromWindow(string w)
        {
            if (string.IsNullOrWhiteSpace(w)) return null;
            w = w.Trim();
            if (Eq(w, "Advertisement") || Eq(w, "Spotify")) return true;
            if (w.Contains(" - ") || w.Contains(" \u2013 ")) return false;
            return null;                                                        // "Spotify Free", "Spotify Premium", anything else
        }

        // Something is playing: the media session says so, or (no media session) Spotify's sound stream is active.
        static bool IsPlaying(AdSignal s) { return s.Session ? s.Playing : s.AudioActive; }

        // Positive proof of music: the window shows "Artist - Song", or (no readable window) the media session has a real artist.
        public static bool SaysMusic(AdSignal s)
        {
            if (s == null) return false;
            var w = FromWindow(s.Window);
            return w == false || (w == null && FromSession(s) == false);
        }

        // Is an ad playing right now (before the debounce)?
        //  - the window is the stronger signal: "Artist - Song" is never an ad (so a song titled "Advertisement" by a real artist
        //    plays on), and a window that says exactly "Advertisement" / "Spotify" while playing is one;
        //  - when the window can't be read (or says something else), the media session decides: no artist (or "Spotify"),
        //    or a playback type that isn't music, is an ad; a real artist with a normal track is not.
        public static bool IsAd(AdSignal s)
        {
            if (s == null || !IsPlaying(s)) return false;                      // paused or silent: nothing to mute
            var w = FromWindow(s.Window);
            if (w != null) return w.Value;
            return FromSession(s) == true;
        }
    }

    // The debounce and the count. Update() is called every poll; it returns true when Active changed.
    public class AdWatch
    {
        public int OnMs = 1200;      // an ad must be seen this long before Spotify is muted (a short glitch never mutes)
        public int OffMs = 1500;     // unclear states (paused, loading) this long before it comes back; real music comes back at once
        public bool Active;          // Spotify is muted for an ad right now
        public int Today;            // ads muted today
        DateTime day = DateTime.MinValue;
        DateTime? adSince, clearSince;

        public bool Update(AdSignal s, DateTime now)
        {
            if (now.Date != day) { day = now.Date; Today = 0; }
            bool ad = AdRules.IsAd(s);
            if (ad)
            {
                clearSince = null;
                if (adSince == null) adSince = now;
                if (!Active && (now - adSince.Value).TotalMilliseconds >= OnMs) { Active = true; Today++; return true; }
                return false;
            }
            adSince = null;
            if (!Active) { clearSince = null; return false; }
            bool gone = s == null || (!s.Session && s.Window == null);        // Spotify closed
            if (gone || AdRules.SaysMusic(s)) { Active = false; clearSince = null; return true; }   // the moment music returns
            if (clearSince == null) clearSince = now;
            if ((now - clearSince.Value).TotalMilliseconds >= OffMs) { Active = false; clearSince = null; return true; }
            return false;
        }

        public void Reset() { Active = false; adSince = null; clearSince = null; }
        // the two fields in /state, the pipe's status and status.json - a yes/no and a count, never what played
        public static void AddStatus(Dictionary<string, object> d, Engine e, AdWatch w, DateTime now)
        {
            d["spotifyAdMuted"] = e.AdMutes(Engine.AdApp);
            d["adsMutedToday"] = w.CountOn(now);
        }
        public int CountOn(DateTime now) { return now.Date == day ? Today : 0; }
    }

    // Reads Spotify's media session and window title (read only - nothing is sent to Spotify).
    public class SpotifySource : IAdSource
    {
        GlobalSystemMediaTransportControlsSessionManager mgr;

        public AdSignal Read()
        {
            var r = new AdSignal { Window = WindowTitle() };
            try
            {
                if (mgr == null) mgr = NowPlaying.Wait(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
                var s = mgr.GetSessions().FirstOrDefault(x => { try { return NowPlaying.KeyFor(x.SourceAppUserModelId) == "spotify"; } catch { return false; } });
                if (s != null)
                {
                    r.Session = true;
                    var pb = s.GetPlaybackInfo();
                    r.Playing = pb.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                    var p = NowPlaying.Wait(s.TryGetMediaPropertiesAsync());
                    if (p != null)
                    {
                        r.Title = p.Title; r.Artist = p.Artist; r.Album = p.AlbumTitle;
                        var t = p.PlaybackType; if (t == null) t = pb.PlaybackType;
                        if (t != null) r.Type = (int)t.Value;
                    }
                }
            }
            catch { mgr = null; }
            return r;
        }

        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);

        // The title of Spotify's main window (also while it's hidden in the tray); null if Spotify isn't running.
        public static string WindowTitle()
        {
            var pids = new HashSet<uint>();
            foreach (var p in Process.GetProcessesByName("Spotify")) { try { pids.Add((uint)p.Id); } catch { } finally { p.Dispose(); } }
            if (pids.Count == 0) return null;
            string visible = null, hidden = null;
            EnumWindows((h, l) =>
            {
                uint pid; GetWindowThreadProcessId(h, out pid);
                if (!pids.Contains(pid) || GetWindow(h, 4) != IntPtr.Zero) return true;   // owned windows (pop-ups) don't count
                var cls = new StringBuilder(64); GetClassName(h, cls, 64);
                if (!cls.ToString().StartsWith("Chrome_WidgetWin", StringComparison.Ordinal)) return true;
                var t = new StringBuilder(256); GetWindowText(h, t, 256);
                var title = t.ToString().Trim(); if (title.Length == 0) return true;
                if (IsWindowVisible(h)) { if (visible == null) visible = title; }
                else if (hidden == null) hidden = title;
                return true;
            }, IntPtr.Zero);
            return visible ?? hidden ?? "";
        }
    }
}
