// Hallow Mixer - command line, for Stream Deck ("Open" action), AutoHotkey, desktop shortcuts and scripts.
//   HallowMixer.exe --preset Gaming            --mute-mic | --unmute-mic | --toggle-mic
//   HallowMixer.exe --channel game 60          --channel chat +10        --mute music | --unmute music | --toggle-mute music
//   HallowMixer.exe --media playpause|next|prev
// If Hallow Mixer is already running, the commands go to it through its local API (this PC only, with its key);
// otherwise it starts and runs them.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web.Script.Serialization;

namespace HallowMixer
{
    public class CliCommand { public string Path; public Dictionary<string, string> Query = new Dictionary<string, string>(); }

    public static class Cli
    {
        static string Ch(string s)
        {
            if (s == null) return null;
            s = s.ToLowerInvariant(); if (s == "system") s = "other";
            return Engine.ChannelIds.Contains(s) ? s : null;
        }
        // Returns the commands, or throws ArgumentException with a message for the user.
        public static List<CliCommand> Parse(string[] args)
        {
            var list = new List<CliCommand>();
            Func<string, CliCommand> C = p => { var c = new CliCommand { Path = p }; list.Add(c); return c; };
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i].ToLowerInvariant();
                Func<string> next = () => { if (i + 1 >= args.Length) throw new ArgumentException(args[i] + " needs a value"); return args[++i]; };
                switch (a)
                {
                    case "--preset": C("/preset").Query["name"] = next(); break;
                    case "--profile": C("/profile").Query["name"] = next(); break;
                    case "--mute-mic": C("/mic").Query["mute"] = "1"; break;
                    case "--unmute-mic": C("/mic").Query["mute"] = "0"; break;
                    case "--toggle-mic": C("/mic").Query["mute"] = "toggle"; break;
                    case "--channel":
                    {
                        var ch = Ch(next()); if (ch == null) throw new ArgumentException("--channel: use game, chat, music, media or system");
                        var v = next(); int n; var c = C("/set"); c.Query["ch"] = ch;
                        if ((v.StartsWith("+") || v.StartsWith("-")) && int.TryParse(v, out n)) c.Query["d"] = n.ToString();
                        else if (int.TryParse(v.TrimEnd('%'), out n) && n >= 0 && n <= 100) c.Query["v"] = n.ToString();
                        else throw new ArgumentException("--channel " + ch + ": use a level 0-100, or +10 / -10");
                        break;
                    }
                    case "--mute": case "--unmute": case "--toggle-mute":
                    {
                        var ch = Ch(next()); if (ch == null) throw new ArgumentException(a + ": use game, chat, music, media or system");
                        var c = C("/mute"); c.Query["ch"] = ch; c.Query["m"] = a == "--mute" ? "1" : a == "--unmute" ? "0" : "toggle";
                        break;
                    }
                    case "--media":
                    {
                        var d = next().ToLowerInvariant(); if (d == "play" || d == "pause") d = "playpause";
                        if (d != "playpause" && d != "next" && d != "prev") throw new ArgumentException("--media: use playpause, next or prev");
                        C("/media").Query["do"] = d; break;
                    }
                    case "--tray": case "--reset": case "--show": break;   // handled elsewhere
                    default: throw new ArgumentException("Unknown option " + args[i] + ". Try --preset, --channel, --mute, --toggle-mic or --media.");
                }
            }
            return list;
        }

        public static string Url(int port, CliCommand c)
        {
            return "http://127.0.0.1:" + port + c.Path + "?" + string.Join("&", c.Query.Select(kv => Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value)));
        }

        // Sends the commands to the running Hallow Mixer. False = couldn't reach it (API off).
        public static bool Send(List<CliCommand> cmds)
        {
            try
            {
                var j = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(MixerApi.FilePath));
                int port = Convert.ToInt32(j["port"]); var key = (string)j["key"];
                foreach (var c in cmds)
                    using (var wc = new WebClient()) { wc.Headers["X-Hallow-Key"] = key; wc.DownloadString(Url(port, c)); }
                return true;
            }
            catch { return false; }
        }
    }
}
