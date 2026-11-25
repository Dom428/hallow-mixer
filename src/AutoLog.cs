// Hallow Mixer 3.29 - "What changed the volume?": every time Mixer turns something down (or back up) by itself it says why, here.
// Real use: "I sometimes hear it try to lower the volume and I don't know why." The sources: ducking for Chat (someone talks in
// Discord) or for your mic, Smart Volume evening out a loud moment, the loud-sound guard, an app sent to the background, per-game
// levels coming back, quiet hours, a Spotify ad. Kept in memory (the last 60) - the window's "Why?" button lists them, the pipe's
// "why" answers with them and status.json carries the last 5. Channel and app names only, never what was playing.
using System;
using System.Collections.Generic;
using System.Linq;

namespace HallowMixer
{
    public class AutoEvent { public DateTime At; public string Kind, Text; }

    public static class AutoLog
    {
        static readonly List<AutoEvent> list = new List<AutoEvent>();
        static readonly Dictionary<string, DateTime> lastOf = new Dictionary<string, DateTime>();
        public static event Action Added;
        public static void Note(string kind, string text) { Note(kind, text, DateTime.Now, 0); }
        // minGapSecs: the same kind+text isn't noted again within that many seconds (Smart Volume can act many times a minute)
        public static bool Note(string kind, string text, DateTime now, double minGapSecs)
        {
            lock (list)
            {
                DateTime last; var k = kind + "|" + text;
                if (minGapSecs > 0 && lastOf.TryGetValue(k, out last) && (now - last).TotalSeconds < minGapSecs) return false;
                lastOf[k] = now;
                list.Add(new AutoEvent { At = now, Kind = kind, Text = text });
                if (list.Count > 60) list.RemoveAt(0);
            }
            var h = Added; if (h != null) try { h(); } catch { }
            return true;
        }
        public static List<AutoEvent> Recent(int n) { lock (list) return list.Skip(Math.Max(0, list.Count - n)).Reverse().ToList(); }
        public static int Count { get { lock (list) return list.Count; } }
        public static void Clear() { lock (list) { list.Clear(); lastOf.Clear(); } }
        public static List<object> Json(int n) { return Recent(n).Select(e => (object)new Dictionary<string, object> { { "at", e.At.ToString("s") }, { "kind", e.Kind }, { "text", e.Text } }).ToList(); }

        // the duck / Smart Volume edges, from the 20-times-a-second tick (pure - the tests drive it)
        public class Edges
        {
            bool chat, mic; readonly Dictionary<string, bool> lowered = new Dictionary<string, bool>();
            public void Duck(bool chatNow, bool micNow, int amount, string talker, DateTime now)
            {
                if (chatNow && !chat) Note("duck-chat", "Game, music and media lowered " + amount + "% - someone talked in Chat" + (talker != null ? " (" + talker + ")" : "") + ". Off: Settings > \"Turn game, music and media down when someone talks in Chat\".", now, 20);
                if (micNow && !mic) Note("duck-mic", "Music and media lowered " + amount + "% - you talked into your mic. Off: Settings > \"Turn music and media down when I talk\".", now, 20);
                chat = chatNow; mic = micNow;
            }
            // gain < 0.8 = Smart Volume has turned the channel down by more than 20%
            public void Level(string channelName, bool on, float gain, DateTime now)
            {
                bool low = on && gain < 0.8f; bool was; lowered.TryGetValue(channelName, out was);
                if (low && !was) Note("smart-volume", channelName + " turned down by Smart Volume (a loud moment - it comes back up over a few seconds). Off: Settings > Smart Volume.", now, 30);
                lowered[channelName] = low;
            }
        }
    }
}
