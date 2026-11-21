// Hallow Mixer - v3.24 per-app volumes in the local API, for Hallow Remote and Hallow Hub on your phone.
// GET /state (and /wait) carry "apps": every app the mixer shows, with its friendly name, channel, level, mute and
// whether it's playing right now. POST /app changes one app exactly like its card in the window does:
// {"key":"spotify","volume":0-100} = its slider, {"key":"spotify","muted":true|false} = clicking its icon,
// {"key":"spotify","channel":"chat"} = right-click > Move to.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace HallowMixer
{
    public static class AppApi
    {
        public static object[] List(Engine eng)
        {
            return eng.Apps.Select(a => (object)new Dictionary<string, object> {
                { "key", a.Key }, { "name", string.IsNullOrEmpty(a.Name) ? a.Key : a.Name }, { "channel", a.Channel },
                { "volume", eng.AppVolume(a.Key) }, { "muted", eng.AppMuted(a.Key) }, { "playing", a.Playing } }).ToArray();
        }

        // The JSON body (and any ?key=...&volume=... in the address) as plain text values; null if the body isn't a JSON object.
        public static Dictionary<string, string> Args(string body, IDictionary<string, string> query)
        {
            var r = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (query != null) foreach (var kv in query) r[kv.Key] = kv.Value;
            if (!string.IsNullOrWhiteSpace(body))
            {
                Dictionary<string, object> j;
                try { j = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body); } catch { return null; }
                if (j == null) return null;
                foreach (var kv in j)
                {
                    var v = kv.Value;
                    r[kv.Key] = v == null ? null : v is bool ? ((bool)v ? "true" : "false") : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            return r;
        }

        // Applies one request. Returns what happened; "!" + text = refused and nothing changed ("!404 " = no such app).
        // moved = the app changed channel (the window rebuilds its lists).
        public static string Do(Engine eng, IDictionary<string, string> args, out bool moved)
        {
            moved = false;
            if (args == null) return "!send JSON like {\"key\":\"spotify\",\"volume\":50}";
            string key, vol, mute, ch;
            args.TryGetValue("key", out key); args.TryGetValue("volume", out vol); args.TryGetValue("muted", out mute); args.TryGetValue("channel", out ch);
            if (string.IsNullOrEmpty(key)) return "!missing key";
            var a = eng.Apps.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            if (a == null) return "!404 no app called " + key + " is in the mixer";
            if (vol == null && mute == null && ch == null) return "!send volume, muted or channel";
            // check everything first, so a bad value changes nothing
            int v = 0; bool m = false;
            if (vol != null)
            {
                double d;
                if (!double.TryParse(vol, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d) || double.IsNaN(d)) return "!volume must be 0-100";
                v = (int)Math.Round(Math.Max(0, Math.Min(100, d)));
            }
            if (mute != null)
            {
                var t = mute.Trim().ToLowerInvariant();
                if (t == "true" || t == "1") m = true;
                else if (t == "false" || t == "0") m = false;
                else if (t == "toggle") m = !eng.AppMuted(a.Key);
                else return "!muted must be true or false";
            }
            if (ch != null && !Engine.ChannelIds.Contains(ch)) return "!channel must be one of " + string.Join(", ", Engine.ChannelIds);
            var done = new List<string>();
            if (ch != null)
            {
                eng.MoveApp(a, ch); moved = true;   // the card's "Move to" (remembered from now on)
                done.Add("moved to " + Engine.ChannelNames[ch]);
            }
            if (vol != null) { eng.SetAppVolume(a.Key, v); done.Add(v + "%"); }   // the card's slider
            if (mute != null) { eng.SetAppMuted(a.Key, m); done.Add(m ? "muted" : "unmuted"); }   // clicking the card's icon
            return a.Name + " " + string.Join(", ", done);
        }
    }
}
