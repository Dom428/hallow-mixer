// Hallow Mixer - channels, which app goes where, the volume math and saved settings.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Microsoft.Win32;

namespace HallowMixer
{
    [DataContract] public class ChannelState
    {
        [DataMember] public string Id;
        [DataMember] public int Volume = 100;
        [DataMember] public bool Muted;
        [DataMember] public string OutputId = "";   // "" = Windows default output
    }
    [DataContract] public class Preset
    {
        [DataMember] public string Name;
        [DataMember] public Dictionary<string, int> Volumes = new Dictionary<string, int>();
        [DataMember] public int ChatMix;
    }
    [DataContract] public class Settings
    {
        [DataMember] public List<ChannelState> Channels = new List<ChannelState>();
        [DataMember] public int ChatMix;                                            // -100 all game ... +100 all chat
        [DataMember] public Dictionary<string, string> AppChannel = new Dictionary<string, string>();  // process name -> channel id (your choices)
        [DataMember] public Dictionary<string, int> AppVolume = new Dictionary<string, int>();         // process name -> 0..100 inside its channel
        [DataMember] public List<Preset> Presets = new List<Preset>();
        [DataMember] public bool TrayTipShown;
        [DataMember] public bool StartMinimized;
        // v1.1
        [DataMember] public bool DuckOnChat = true;       // someone talks in Chat -> game, music and media go down
        [DataMember] public int DuckAmount = 50;          // ... by this many percent
        [DataMember] public bool DuckOnMic;               // you talk -> music and media go down
        [DataMember] public bool PerGame = true;          // remember channel levels for each game
        [DataMember] public bool Overlay = true;          // on-screen pop-up for hotkeys
        [DataMember] public List<string> MutedApps = new List<string>();
        [DataMember] public Dictionary<string, Preset> GameLevels = new Dictionary<string, Preset>();
        // v1.2
        [DataMember] public bool AutoSwitch;              // headset first: use PrimaryOut whenever it's on, else FallbackOut
        [DataMember] public string PrimaryOut = "";
        [DataMember] public string FallbackOut = "";
        [DataMember] public string PrimaryMic = "";       // v3.11: kept as the default mic whenever it's plugged in (with AutoSwitch)
        [DataMember] public double MiniLeft = -1, MiniTop = -1;
        [DataMember] public bool MiniOpen;
        // v1.3: EQ per device id (through Equalizer APO)
        [DataMember] public Dictionary<string, EqState> Eq = new Dictionary<string, EqState>();
        // v1.4: process name -> device id, for an app that should play somewhere other than its channel
        [DataMember] public Dictionary<string, string> AppOutput = new Dictionary<string, string>();
        // v1.5
        [DataMember] public string MicMode = "off";      // "off", "ptt" (push to talk), "ptm" (push to mute)
        [DataMember] public int PttKey = 0x05;            // virtual key; 0x05 = mouse back side button
        [DataMember] public double WinLeft = -1, WinTop = -1, WinWidth = -1, WinHeight = -1;
        [DataMember] public bool WinMax;
        // v1.6
        [DataMember] public bool AutoUpdate = true;
        [DataMember] public string LastUpdateCheck = "";
        // v1.7
        [DataMember] public bool MicKeep;                 // put the mic level back if Discord, a game or Windows changes it
        [DataMember] public int MicKeepLevel = -1;        // 0..100, -1 = take the current level
        // v1.8
        [DataMember] public bool EqPerGame = true;        // each game remembers its headset/speaker EQ
        [DataMember] public Dictionary<string, Dictionary<string, EqState>> GameEq = new Dictionary<string, Dictionary<string, EqState>>();
        [DataMember] public bool LevelMedia;              // even out loud and quiet moments in the Media channel
        // v3.21 - Smart Volume on more channels (Media stays on LevelMedia)
        [DataMember] public List<string> LevelOn = new List<string>();   // "game", "chat", "music"
        [DataMember] public bool LoudnessMeter = true;
        // v3.22 - app rules: an app always starts at this level (process name -> 0..100); names of remembered apps that aren't running
        [DataMember] public Dictionary<string, int> AppStart = new Dictionary<string, int>();
        [DataMember] public Dictionary<string, string> AppNames = new Dictionary<string, string>();
        // v3.20 - quick output switching
        [DataMember] public Dictionary<string, Dictionary<string, string>> OutputsByHeadset = new Dictionary<string, Dictionary<string, string>>();   // default output -> channel -> output ("" = default)
        [DataMember] public bool OutputKey = true;          // Ctrl+Alt+O = next output
        // v3.19 - your own EQ presets
        [DataMember] public List<EqUserPreset> MyEq = new List<EqUserPreset>();
        // v3.18 - sidetone
        [DataMember] public bool SidetoneOn;               // hear your own mic in your headset (off by default)
        [DataMember] public int SidetoneLevel = 30;        // 0..100
        [DataMember] public string SidetoneMic = "";       // "" = Windows' default mic
        // v3.17
        [DataMember] public bool UseHallowAccent = true;   // the family accent colour from Hallow Toolkit (theme.json)
        [DataMember] public string LookMode = "hallow";    // v3.25 Look: "hallow" (follow Hallow Toolkit's light / dark), "light" or "dark"
        [DataMember] public string LastCrashShown = "";    // the crash report we already told you about
        // v3.15
        [DataMember] public bool UseHallowIcon = true;     // hide the tray icon while Hallow Hub shows one icon for every Hallow app
        // v3.14 - mic presets per game: game key -> mic EQ preset name (Eq.MicPresets or PEq.MicPresets)
        [DataMember] public Dictionary<string, string> GameMic = new Dictionary<string, string>();
        [DataMember] public Dictionary<string, string> GameChain = new Dictionary<string, string>();   // v3.16: game key -> mic chain preset
        [DataMember] public string GameMicOn;             // the game mic preset on the mic right now (null = your normal mic EQ)
        [DataMember] public string GameMicDevice = "";    // the mic it's on
        [DataMember] public EqState GameMicNormal;        // that mic's own EQ, put back when the game closes (null = it had none)
        // v3.13
        [DataMember] public bool StartNoticeOff;         // "doesn't start with Windows" notice dismissed (or start-up turned off on purpose)
        // v3.9
        [DataMember] public string LastSeenVersion = "";   // for "What's new" after an update
        // v3.7 - split headset
        [DataMember] public bool Split;
        [DataMember] public string SplitPrev = "";       // Chat's output before
        // v3.6
        [DataMember] public string ProfileName = "Default";
        public static bool Frozen;                       // set while switching profile / restoring a backup
        // v3.5 - quiet hours
        [DataMember] public bool QuietOn;
        [DataMember] public int QuietFrom = 23, QuietTo = 7;   // hours, 0..23 (can wrap past midnight)
        [DataMember] public int QuietMax = 40;                 // master volume cap in %
        // v3.3
        [DataMember] public bool MutedWarn = true;      // pop-up if you talk while your mic is muted
        // v3.2
        [DataMember] public bool Guard;                 // loud-sound guard: sudden blasts turn their channel down for a moment
        // v3.1
        [DataMember] public Dictionary<string, int> BgLevel = new Dictionary<string, int>();   // app -> 0..99 % while you're in another app (0 = muted)
        // v2.8
        [DataMember] public bool PresetKeys = true;     // Ctrl+Alt+1..9 = preset 1..9, Ctrl+Alt+0 = next preset
        // v2.6
        [DataMember] public bool SmartDuck = true;      // duck only for real talking, not join/leave sounds or short noises
        // v2.5
        [DataMember] public bool NowPlayingOn = true;   // show the song / video that's playing, with play/pause and skip
        // v2.3 - clean chat voices (NVIDIA Broadcast speaker effects)
        [DataMember] public bool ChatClean;
        [DataMember] public string ChatCleanPrev = "";   // the Chat channel's output before, to go back to
        // v2.2 - first-run setup
        [DataMember] public bool SetupDone;
        [DataMember] public string SetupMode = "";       // "sonar", "replace", "mixer"
        public static bool FirstRun;                     // no settings file when Hallow Mixer started (a new install)
        // v2.1
        [DataMember] public bool ApiOn = true;            // Hallow Toolkit + phone dashboard can control the mixer (this PC only, with a key)
        // v2.0 - streamers (OBS)
        [DataMember] public bool ObsAuto;
        [DataMember] public string ObsHost = "localhost";
        [DataMember] public int ObsPort = 4455;
        [DataMember] public string ObsPass = "";          // encrypted for this Windows user (DPAPI)
        [DataMember] public Dictionary<string, List<string>> StreamLinks = new Dictionary<string, List<string>>();   // channel -> OBS source names
        [DataMember] public Dictionary<string, int> StreamLevel = new Dictionary<string, int>();                    // channel -> 0..100 on stream
        [DataMember] public List<string> StreamOff = new List<string>();                                             // channels kept off stream
        [DataMember] public Dictionary<string, string> ScenePresets = new Dictionary<string, string>();             // OBS scene -> preset name

        // The JSON reader skips field initialisers, so set the defaults here (values in the file then override them).
        [OnDeserializing] void Defaults(StreamingContext c)
        {
            DuckOnChat = true; DuckAmount = 50; PerGame = true; Overlay = true;
            PrimaryOut = ""; FallbackOut = ""; PrimaryMic = ""; MiniLeft = -1; MiniTop = -1;
            Eq = new Dictionary<string, EqState>(); AppOutput = new Dictionary<string, string>();
            AutoUpdate = true; LastUpdateCheck = ""; MicKeepLevel = -1; ApiOn = true; UseHallowIcon = true; UseHallowAccent = true; LookMode = "hallow"; LastCrashShown = ""; SidetoneLevel = 30; SidetoneMic = ""; MyEq = new List<EqUserPreset>(); OutputsByHeadset = new Dictionary<string, Dictionary<string, string>>(); OutputKey = true; LevelOn = new List<string>(); LoudnessMeter = true; NowPlayingOn = true; SmartDuck = true; PresetKeys = true; MutedWarn = true; QuietFrom = 23; QuietTo = 7; QuietMax = 40; ProfileName = "Default"; SplitPrev = ""; LastSeenVersion = ""; BgLevel = new Dictionary<string, int>(); SetupMode = ""; ChatCleanPrev = ""; ObsHost = "localhost"; ObsPort = 4455; ObsPass = ""; EqPerGame = true; GameEq = new Dictionary<string, Dictionary<string, EqState>>(); MicMode = "off"; PttKey = 0x05; WinLeft = -1; WinTop = -1; WinWidth = -1; WinHeight = -1;
            MutedApps = new List<string>(); GameLevels = new Dictionary<string, Preset>(); GameMic = new Dictionary<string, string>(); GameChain = new Dictionary<string, string>(); GameMicDevice = "";
            AppChannel = new Dictionary<string, string>(); AppVolume = new Dictionary<string, int>();
            AppStart = new Dictionary<string, int>(); AppNames = new Dictionary<string, string>();
        }

        static string Dir { get { return Program.DataDir; } }
        static string FilePath { get { return Path.Combine(Dir, "settings.json"); } }
        public static string FilePathPublic { get { Directory.CreateDirectory(Dir); return FilePath; } }
        // A backup file is accepted only if it reads as Hallow Mixer settings with channels in it.
        public static bool LooksValid(string file)
        {
            try { using (var f = File.OpenRead(file)) { var s = (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(f); return s != null && s.Channels != null && s.Channels.Count > 0; } }
            catch { return false; }
        }
        public static Settings Load()
        {
            Settings s = null;
            FirstRun = !File.Exists(FilePath);
            try { if (File.Exists(FilePath)) using (var f = File.OpenRead(FilePath)) s = (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(f); } catch { }
            if (s == null) s = new Settings();
            if (s.Channels == null) s.Channels = new List<ChannelState>();
            if (s.AppChannel == null) s.AppChannel = new Dictionary<string, string>();
            if (s.AppVolume == null) s.AppVolume = new Dictionary<string, int>();
            if (s.Presets == null || s.Presets.Count == 0) s.Presets = DefaultPresets();
            if (s.MutedApps == null) s.MutedApps = new List<string>();
            if (s.Eq == null) s.Eq = new Dictionary<string, EqState>();
            if (s.AppOutput == null) s.AppOutput = new Dictionary<string, string>();
            if (s.AppStart == null) s.AppStart = new Dictionary<string, int>();
            if (s.AppNames == null) s.AppNames = new Dictionary<string, string>();
            if (s.GameLevels == null) s.GameLevels = new Dictionary<string, Preset>();
            if (s.GameEq == null) s.GameEq = new Dictionary<string, Dictionary<string, EqState>>();
            if (s.GameMic == null) s.GameMic = new Dictionary<string, string>();
            if (s.GameChain == null) s.GameChain = new Dictionary<string, string>();
            if (s.MyEq == null) s.MyEq = new List<EqUserPreset>();
            if (s.LevelOn == null) s.LevelOn = new List<string>();
            if (s.OutputsByHeadset == null) s.OutputsByHeadset = new Dictionary<string, Dictionary<string, string>>();
            if (s.GameMicDevice == null) s.GameMicDevice = "";
            if (s.StreamLinks == null) s.StreamLinks = new Dictionary<string, List<string>>();
            if (s.StreamLevel == null) s.StreamLevel = new Dictionary<string, int>();
            if (s.StreamOff == null) s.StreamOff = new List<string>();
            if (s.ScenePresets == null) s.ScenePresets = new Dictionary<string, string>();
            if (s.SetupMode == null) s.SetupMode = "";
            if (Profiles.Clean(s.ProfileName) == null) s.ProfileName = "Default";
            if (s.BgLevel == null) s.BgLevel = new Dictionary<string, int>();
            if (s.ChatCleanPrev == null) s.ChatCleanPrev = "";
            if (s.SplitPrev == null) s.SplitPrev = "";
            if (string.IsNullOrEmpty(s.ObsHost)) s.ObsHost = "localhost";
            if (s.ObsPort <= 0) s.ObsPort = 4455;
            if (s.DuckAmount <= 0 || s.DuckAmount > 100) s.DuckAmount = 50;
            foreach (var c in Engine.ChannelIds) if (!s.Channels.Any(x => x.Id == c)) s.Channels.Add(new ChannelState { Id = c });
            return s;
        }
        public void Save()
        {
            if (Frozen) return;
            try
            {
                Directory.CreateDirectory(Dir);
                var tmp = FilePath + ".tmp";
                using (var f = File.Create(tmp)) new DataContractJsonSerializer(typeof(Settings)).WriteObject(f, this);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch { }
        }
        static List<Preset> DefaultPresets()
        {
            Func<string, int, int, int, int, int, int, Preset> p = (n, g, c, m, me, o, mix) =>
            {
                var x = new Preset { Name = n, ChatMix = mix };
                x.Volumes["game"] = g; x.Volumes["chat"] = c; x.Volumes["music"] = m; x.Volumes["media"] = me; x.Volumes["other"] = o;
                return x;
            };
            return new List<Preset> { p("Gaming", 100, 100, 35, 60, 70, 0), p("Movie", 30, 60, 40, 100, 50, 0), p("Chill", 40, 85, 100, 70, 70, 0) };
        }
    }

    // One app (process) as shown in the mixer. It can have several sessions (one per output device).
    public class MixApp
    {
        public HashSet<uint> Pids = new HashSet<uint>();
        public string Key;          // process name, lower case ("system" for Windows sounds)
        public string Name;         // friendly name
        public string ExePath;
        public string Channel;
        public bool AutoChannel;    // true = guessed, false = you moved it
        public List<AppSession> Sessions = new List<AppSession>();
        internal List<AppSession> Prev;   // last refresh's streams (to carry over what we set)
        public float Peak() { float m = 0; foreach (var s in Sessions) m = Math.Max(m, s.Peak()); return m; }
        public bool Playing { get { foreach (var s in Sessions) if (s.Active()) return true; return false; } }   // v3.24 any of its streams active
    }

    public class Engine
    {
        public static readonly string[] ChannelIds = { "game", "chat", "music", "media", "other" };
        public static readonly Dictionary<string, string> ChannelNames = new Dictionary<string, string> {
            { "game", "Game" }, { "chat", "Chat" }, { "music", "Music" }, { "media", "Media" }, { "other", "System & other" } };

        static readonly Dictionary<string, string> Known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "discord", "chat" }, { "discordptb", "chat" }, { "discordcanary", "chat" }, { "teamspeak", "chat" }, { "ts3client_win64", "chat" }, { "ts3client_win32", "chat" },
            { "mumble", "chat" }, { "slack", "chat" }, { "ms-teams", "chat" }, { "teams", "chat" }, { "zoom", "chat" }, { "skype", "chat" }, { "guilded", "chat" }, { "element", "chat" }, { "signal", "chat" }, { "whatsapp", "chat" },
            { "spotify", "music" }, { "applemusic", "music" }, { "itunes", "music" }, { "tidal", "music" }, { "deezer", "music" }, { "amazon music", "music" }, { "foobar2000", "music" },
            { "musicbee", "music" }, { "aimp", "music" }, { "winamp", "music" }, { "youtube music", "music" }, { "soundcloud", "music" },
            { "chrome", "media" }, { "msedge", "media" }, { "brave", "media" }, { "firefox", "media" }, { "opera", "media" }, { "opera_gx", "media" }, { "vivaldi", "media" }, { "arc", "media" },
            { "vlc", "media" }, { "mpc-hc64", "media" }, { "mpc-be64", "media" }, { "potplayer64", "media" }, { "potplayermini64", "media" }, { "jellyfinmediaplayer", "media" },
            { "plex", "media" }, { "plexamp", "music" }, { "mpv", "media" }, { "netflix", "media" }, { "obs64", "other" }, { "medal", "other" }
        };

        public Settings S;
        public List<MixApp> Apps = new List<MixApp>();
        readonly List<string> gameRoots = new List<string>();
        readonly Dictionary<uint, string> routed = new Dictionary<uint, string>();   // pid -> device id we routed it to
        public string LastError;

        public Engine()
        {
            S = Settings.Load();
            FindGameRoots();
        }

        public ChannelState Chan(string id) { return S.Channels.First(c => c.Id == id); }

        // ---- v3.20 each headset remembers where every channel plays. Called when Windows' default output changes:
        // the channels' outputs are kept for the old one, and the new one's come back (if it has any yet).
        // Chat is left alone while Split headset or Clean voices manage it. Returns true when a channel moved.
        public bool SwitchHeadset(string oldDefault, string newDefault)
        {
            if (string.IsNullOrEmpty(newDefault) || oldDefault == newDefault) return false;
            Func<string, bool> managed = id => id == "chat" && (S.Split || S.ChatClean);
            if (!string.IsNullOrEmpty(oldDefault))
                S.OutputsByHeadset[oldDefault] = S.Channels.Where(c => !managed(c.Id)).ToDictionary(c => c.Id, c => c.OutputId ?? "");
            Dictionary<string, string> saved; bool moved = false;
            if (!S.OutputsByHeadset.TryGetValue(newDefault, out saved)) return false;
            foreach (var c in S.Channels)
            {
                string o; if (managed(c.Id) || !saved.TryGetValue(c.Id, out o)) continue;
                if ((c.OutputId ?? "") != o) { c.OutputId = o; moved = true; }
            }
            while (S.OutputsByHeadset.Count > 20) S.OutputsByHeadset.Remove(S.OutputsByHeadset.Keys.First(k => k != newDefault));
            return moved;
        }
        // The output after the current one (Ctrl+Alt+O), or null if there's only one.
        public static AudioDevice NextOutput(List<AudioDevice> outs, string current)
        {
            if (outs == null || outs.Count < 2) return null;
            int i = outs.FindIndex(d => d.Id == current);
            return outs[(i + 1) % outs.Count];
        }

        void FindGameRoots()
        {
            try
            {
                var steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
                if (steam != null)
                {
                    steam = steam.Replace('/', '\\');
                    AddRoot(Path.Combine(steam, @"steamapps\common"));
                    var vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
                    if (File.Exists(vdf))
                        foreach (var line in File.ReadAllLines(vdf))
                        {
                            var t = line.Trim();
                            if (!t.StartsWith("\"path\"")) continue;
                            var parts = t.Split('"'); if (parts.Length >= 4) AddRoot(Path.Combine(parts[3].Replace(@"\\", @"\"), @"steamapps\common"));
                        }
                }
            }
            catch { }
            foreach (var d in DriveInfo.GetDrives())
            {
                if (d.DriveType != DriveType.Fixed) continue;
                foreach (var sub in new[] { "XboxGames", "Xbox", @"Program Files\Epic Games", "Epic Games", "Riot Games", "Games", @"Program Files\EA Games", @"Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games" })
                    AddRoot(Path.Combine(d.RootDirectory.FullName, sub));
            }
        }
        void AddRoot(string p) { try { if (Directory.Exists(p) && !gameRoots.Contains(p, StringComparer.OrdinalIgnoreCase)) gameRoots.Add(p.TrimEnd('\\')); } catch { } }

        string Guess(string key, string exe)
        {
            string c;
            if (S.AppChannel.TryGetValue(key, out c) && ChannelIds.Contains(c)) return c;
            if (Known.TryGetValue(key, out c)) return c;
            if (exe != null)
            {
                foreach (var r in gameRoots)
                    if (exe.StartsWith(r + "\\", StringComparison.OrdinalIgnoreCase) && exe.IndexOf(@"\WindowsApps\Microsoft.", StringComparison.OrdinalIgnoreCase) < 0) return "game";
            }
            return "other";
        }

        // v3.23 a readable name: the program's own description ("Discord", "Google Chrome"), else its file name made pretty
        // ("DeltaForceClient-Win64-Shipping" -> "Delta Force").
        public static string Friendly(string exe, string key)
        {
            string file = exe != null ? Path.GetFileNameWithoutExtension(exe) : key;
            try
            {
                if (exe != null && File.Exists(exe))
                {
                    var fi = FileVersionInfo.GetVersionInfo(exe);
                    foreach (var cand in new[] { fi.FileDescription, fi.ProductName })
                    {
                        if (string.IsNullOrWhiteSpace(cand)) continue;
                        var n = cand.Trim();
                        if (n.Length >= 40 || n.Length < 2 || Junk(n)) continue;
                        // a description that is just the file name again gets the same clean-up as the file name
                        if (n.IndexOf(' ') < 0 && Squash(n) == Squash(file)) return PrettyName(n);
                        return n;
                    }
                }
            }
            catch { }
            return PrettyName(file ?? key ?? "");
        }
        static bool Junk(string n)
        {
            var l = n.ToLowerInvariant();
            return l.Contains("shipping") || l == "unreal engine" || l == "unrealgame" || l == "bootstrapper" || l == "launcher" || l == "application" || l.EndsWith(".exe");
        }
        static string Squash(string s) { return new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant(); }
        static readonly System.Text.RegularExpressions.Regex[] Tails = {
            new System.Text.RegularExpressions.Regex(@"[-_. ]?(win64|win32|wingdk|x64|x86|64bit|32bit)?[-_. ]?shipping$", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            new System.Text.RegularExpressions.Regex(@"[-_. ](win64|win32|x64|x86|64|32|dx11|dx12|vulkan|vk|be|eac)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            new System.Text.RegularExpressions.Regex(@"_(x64|x86)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            new System.Text.RegularExpressions.Regex(@"(?<=[a-z]{3})[-_ ]?client$", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
        };
        // "DeltaForceClient-Win64-Shipping" -> "Delta Force", "my_game_x64" -> "My Game", "obs64" -> "Obs64"
        public static string PrettyName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw ?? "";
            var s = raw.Trim();
            if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 4);
            for (int pass = 0; pass < 3; pass++)
                foreach (var t in Tails) { var n = t.Replace(s, ""); if (n.Length >= 2) s = n; }
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '_' || c == '-' || c == '.') { sb.Append(' '); continue; }
                // a space before an upper-case letter that follows a lower-case one ("DeltaForce" -> "Delta Force")
                if (i > 0 && char.IsUpper(c) && char.IsLower(s[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            var words = sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1));
            var r = string.Join(" ", words);
            return r.Length > 0 ? r : raw;
        }
        // v3.23 the channel an app goes in by its name alone (for now playing when the app has no sound stream right now)
        public static string KnownChannel(string key)
        {
            string c; if (string.IsNullOrEmpty(key)) return null;
            if (Known.TryGetValue(key, out c)) return c;
            foreach (var kv in Known) if (kv.Key.Length > 3 && key.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            return null;
        }

        readonly Dictionary<uint, string> nameCache = new Dictionary<uint, string>();
        string ProcName(uint pid)
        {
            string n;
            if (nameCache.TryGetValue(pid, out n)) return n;
            try { using (var p = Process.GetProcessById((int)pid)) n = p.ProcessName.ToLowerInvariant(); } catch { n = null; }
            if (nameCache.Count > 500) nameCache.Clear();
            nameCache[pid] = n; return n;
        }
        // pid -> exe path, so we only look each process up once
        readonly Dictionary<uint, string> exeCache = new Dictionary<uint, string>();

        // Re-reads every app stream and groups them by app (one row for Discord even if it runs several processes).
        // Returns true when the list of apps changed.
        public bool Refresh()
        {
            List<AppSession> sessions;
            try { sessions = CoreAudio.Sessions(); } catch (Exception e) { LastError = e.Message; return false; }
            var byKey = new Dictionary<string, MixApp>();
            var old = Apps.ToDictionary(x => x.Key);
            foreach (var s in sessions)
            {
                string exe = null, key;
                if (s.IsSystem || s.Pid == 0) key = "system";
                else
                {
                    if (!exeCache.TryGetValue(s.Pid, out exe)) { exe = Proc.Path(s.Pid); exeCache[s.Pid] = exe; }
                    // v3.23: Windows won't give the path of some processes (games with anti-cheat, apps run as admin) -
                    // their process name still sorts them right ("spotify" -> Music) instead of a "pid1234" in System & other
                    key = exe != null ? Path.GetFileNameWithoutExtension(exe).ToLowerInvariant() : (ProcName(s.Pid) ?? ("pid" + s.Pid));
                }
                MixApp app;
                if (!byKey.TryGetValue(key, out app))
                {
                    MixApp prev;
                    if (old.TryGetValue(key, out prev)) { app = prev; app.Prev = app.Sessions; app.Sessions = new List<AppSession>(); app.Pids = new HashSet<uint>(); }
                    else
                    {
                        app = new MixApp { Key = key, ExePath = exe, Pids = new HashSet<uint>() };
                        app.Name = key == "system" ? "Windows sounds" : Friendly(exe, key);
                        app.AutoChannel = !S.AppChannel.ContainsKey(key);
                        app.Channel = Guess(key, exe);
                    }
                    byKey[key] = app;
                }
                if (key != "system") app.Pids.Add(s.Pid);
                // keep what we last set on a stream we already know, so we don't re-apply it every refresh
                if (app.Prev != null)
                {
                    var p = app.Prev.FirstOrDefault(x => x.DeviceId == s.DeviceId && x.Pid == s.Pid);
                    if (p != null) { s.LastSet = p.LastSet; s.LastMute = p.LastMute; }
                }
                app.Sessions.Add(s);
            }
            bool changed = byKey.Count != Apps.Count || byKey.Keys.Any(k => !old.ContainsKey(k));
            Apps = byKey.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var a in Apps) if (!old.ContainsKey(a.Key)) AppStarted(a);
            refreshes++;
            if (exeCache.Count > 500) exeCache.Clear();
            if (CheckGame()) LevelsSwitched = true;
            Apply();
            return changed;
        }
        public float ChannelFactor(string ch)
        {
            int mix = S.ChatMix;
            if (ch == "game" && mix > 0) return 1f - mix / 100f;
            if (ch == "chat" && mix < 0) return 1f + mix / 100f;
            return 1f;
        }
        public int AppVolume(string key) { int v; return S.AppVolume.TryGetValue(key, out v) ? v : 100; }
        public bool AppMuted(string key) { return S.MutedApps.Contains(key); }
        // v3.24 the app card's slider (and POST /app): its level inside the channel; 100 = nothing stored. Saved later by the window.
        public void SetAppVolume(string key, int level)
        {
            level = Math.Max(0, Math.Min(100, level));
            if (level == 100) S.AppVolume.Remove(key); else S.AppVolume[key] = level;
            Apply();
        }
        // ---- v3.1 background mute: the app whose window you're in (process name), set by the window
        public string FrontKey;
        public float BgFactor(string key)
        {
            int l; if (key == null || !S.BgLevel.TryGetValue(key, out l) || key == FrontKey) return 1f;
            return Math.Max(0, Math.Min(99, l)) / 100f;
        }
        public bool InBackground(string key) { return S.BgLevel.ContainsKey(key) && key != FrontKey; }
        public void ToggleAppMute(MixApp a)
        {
            if (!S.MutedApps.Remove(a.Key)) S.MutedApps.Add(a.Key);
            S.Save(); Apply();
        }

        // ---- auto-ducking: someone talks in Chat (or you talk) -> other channels go down, then fade back
        public float DuckChat = 1f, DuckMic = 1f;          // current multipliers (1 = not ducked)
        public readonly VoiceDetector ChatVoice = new VoiceDetector(0.03f), MicVoice = new VoiceDetector(0.08f);
        public bool Ducking { get { return DuckChat < 0.97f || DuckMic < 0.97f; } }
        // Called ~20 times a second with the live levels. Returns true when the volumes need re-applying.
        public bool UpdateDuck(float chatPeak, float micPeak) { return UpdateDuck(chatPeak, micPeak, DateTime.UtcNow); }
        public bool UpdateDuck(float chatPeak, float micPeak, DateTime now)
        {
            ChatVoice.Smart = MicVoice.Smart = S.SmartDuck;
            bool chatTalk = ChatVoice.Update(chatPeak, now), micTalk = MicVoice.Update(micPeak, now);
            float low = 1f - S.DuckAmount / 100f;
            float tChat = S.DuckOnChat && chatTalk ? low : 1f;
            float tMic = S.DuckOnMic && micTalk ? low : 1f;
            float oc = DuckChat, om = DuckMic;
            DuckChat += (tChat - DuckChat) * (tChat < DuckChat ? 0.45f : 0.07f);   // down fast, back up gently
            DuckMic += (tMic - DuckMic) * (tMic < DuckMic ? 0.45f : 0.07f);
            if (Math.Abs(DuckChat - tChat) < 0.005f) DuckChat = tChat;
            if (Math.Abs(DuckMic - tMic) < 0.005f) DuckMic = tMic;
            return Math.Abs(oc - DuckChat) > 0.004f || Math.Abs(om - DuckMic) > 0.004f;
        }
        // ---- v3.2 loud-sound guard. The meters read each app before its volume, so a jump is seen the same at any fader level.
        readonly Dictionary<string, float> guardEnv = new Dictionary<string, float>(), guardGain = new Dictionary<string, float>();
        readonly Dictionary<string, DateTime> guardHold = new Dictionary<string, DateTime>();
        public string GuardNote;   // set when the guard catches something (the window shows it)
        // Called ~20 times a second with each channel's peak. Returns true when volumes need re-applying.
        public bool UpdateGuard(Dictionary<string, float> peaks, DateTime now)
        {
            bool changed = false;
            foreach (var ch in ChannelIds)
            {
                float pk; peaks.TryGetValue(ch, out pk);
                float env; if (!guardEnv.TryGetValue(ch, out env)) env = pk;
                float g; if (!guardGain.TryGetValue(ch, out g)) g = 1f;
                DateTime hold; guardHold.TryGetValue(ch, out hold);
                float old = g;
                if (S.Guard && pk > 0.7f && pk > env * 3.5f && env < 0.25f)
                {
                    // a sudden blast: far above what this channel was just playing
                    g = Math.Min(g, 0.35f); guardHold[ch] = now.AddMilliseconds(600);
                    GuardNote = ChannelNames[ch] + ": sudden loud sound turned down";
                }
                else if (!S.Guard) g = 1f;
                else if (now > hold) g += (1f - g) * 0.06f;          // ease back over about 2 seconds
                if (g > 0.995f) g = 1f;
                guardGain[ch] = g;
                guardEnv[ch] = env * 0.97f + pk * 0.03f;               // what's "normal" for this channel (about 1.5 s)
                if (Math.Abs(old - g) > 0.004f) changed = true;
            }
            return changed;
        }
        // ---- v3.5 quiet hours: from..to (wraps past midnight); while on, Windows sounds (System & other) are halved
        // v3.11: a device appeared in the last 8 seconds, so a default change now is Windows' doing, not yours.
        public static bool StolenBy(DateTime arrived, DateTime now) { var s = (now - arrived).TotalSeconds; return s >= 0 && s <= 8; }

        public static bool InQuiet(int hour, int from, int to)
        {
            if (from == to) return false;
            return from < to ? hour >= from && hour < to : hour >= from || hour < to;
        }
        public bool QuietNow;
        public float GuardFactor(string ch) { float g; return guardGain.TryGetValue(ch, out g) ? g : 1f; }
        float DuckFactor(string ch)
        {
            float f = GuardFactor(ch);
            if (QuietNow && ch == "other") f *= 0.5f;
            if (ch == "game" || ch == "music" || ch == "media") f *= DuckChat;
            if (ch == "music" || ch == "media") f *= DuckMic;
            f *= LevelGain(ch);
            return f;
        }

        // ---- loudness levelling for Media: loud moments (ads, explosions) go down, quiet ones come up a little.
        // Windows reports each app's level before its volume is applied, so this always sees the real loudness.
        // v3.21 Smart Volume: the same levelling for Game, Chat and Music too, each with its own range -
        // Game keeps more of its punch, Chat lifts quiet friends the most.
        readonly Dictionary<string, float> levelGain = new Dictionary<string, float>(), levelEnv = new Dictionary<string, float>();
        public static readonly string[] LevelChannels = { "game", "chat", "music", "media" };
        public bool Levelled(string ch) { return ch == "media" ? S.LevelMedia : S.LevelOn.Contains(ch); }
        public float LevelGain(string ch) { float g; return levelGain.TryGetValue(ch, out g) ? g : 1f; }
        public float LevelMediaGain { get { return LevelGain("media"); } }
        static void Range(string ch, out float lo, out float hi)
        {
            switch (ch) { case "game": lo = 0.45f; hi = 1.25f; return; case "chat": lo = 0.4f; hi = 1.6f; return; case "music": lo = 0.4f; hi = 1.3f; return; default: lo = 0.3f; hi = 1.4f; return; }
        }
        public bool UpdateLevel(float mediaPeak) { return UpdateLevel("media", mediaPeak); }
        public bool UpdateLevel(string ch, float peak)
        {
            float old = LevelGain(ch), env; levelEnv.TryGetValue(ch, out env);
            if (!Levelled(ch)) { levelEnv[ch] = 0; levelGain[ch] = 1f; return Math.Abs(old - 1f) > 0.004f; }
            env += (peak - env) * (peak > env ? 0.7f : 0.015f);   // follows loud parts fast, lets go slowly
            levelEnv[ch] = env;
            float g = old;
            if (env > 0.02f)   // only while something is playing (silence keeps the last setting)
            {
                float lo, hi; Range(ch, out lo, out hi);
                float target = Math.Max(lo, Math.Min(hi, 0.5f / env));
                g += (target - g) * (target < g ? 0.55f : 0.02f);
            }
            levelGain[ch] = g;
            return Math.Abs(old - g) > 0.004f;
        }

        // ---- EQ per game: a game's headset/speaker EQ comes back when it starts; your normal EQ when it closes. Mic EQ is never touched.
        Dictionary<string, EqState> eqNormal;
        public bool EqSwitched;
        static EqState CloneEq(EqState e) { return new EqState { Name = e.Name, Capture = e.Capture, On = e.On, Preset = e.Preset, LowCut = e.LowCut, Space = e.Space, Gains = e.Gains == null ? new int[10] : (int[])e.Gains.Clone(), Param = e.Param, PBands = e.PBands == null ? null : PEq.Copy(e.PBands), Chain = e.Chain == null ? null : e.Chain.Copy() }; }
        Dictionary<string, EqState> OutputEq() { return S.Eq.Where(kv => !kv.Value.Capture).ToDictionary(kv => kv.Key, kv => CloneEq(kv.Value)); }
        void UseOutputEq(Dictionary<string, EqState> src)
        {
            foreach (var k in S.Eq.Where(kv => !kv.Value.Capture).Select(kv => kv.Key).ToList()) S.Eq.Remove(k);
            foreach (var kv in src) S.Eq[kv.Key] = CloneEq(kv.Value);
            EqSwitched = true;
        }
        void SwitchEq(string key)
        {
            if (!S.EqPerGame) return;
            if (CurrentGame == null && key != null) eqNormal = OutputEq();
            Dictionary<string, EqState> saved;
            if (key != null && S.GameEq.TryGetValue(key, out saved)) UseOutputEq(saved);
            else if (eqNormal != null && (key == null || CurrentGame != null)) UseOutputEq(eqNormal);   // game closed, or a game without its own EQ
            if (key == null) eqNormal = null;
            if (EqSwitched) Eq.Write(S.Eq);
        }
        // The EQ window calls this after a change: while a game runs, the change is remembered for that game.
        public string NoteEqChanged()
        {
            if (!S.EqPerGame || CurrentGame == null) return null;
            S.GameEq[CurrentGame] = OutputEq();
            return CurrentGameName;
        }

        // ---- v3.14 mic presets per game: a game can put a mic EQ preset on your mic while it runs; your own mic EQ comes back when it closes.
        // What was on the mic before is kept in the settings, so it comes back even if Hallow Mixer was closed or restarted mid-game.
        public Func<AudioDevice> MicDevice;   // the real mic (not Sonar's virtual one) - set by the window
        public string MicNote;                // "QuadCast 2S mic preset on for Valorant" - shown by the window
        public static List<string> MicPresetNames() { return Eq.MicPresets.Keys.Concat(PEq.MicPresets.Keys).ToList(); }
        static bool IsMicPreset(string n) { return n != null && (Eq.MicPresets.ContainsKey(n) || PEq.MicPresets.ContainsKey(n)); }
        void SwitchMic(string key, string game)
        {
            string wantEq = null, wantChain = null;   // v3.16: a game can also have a mic chain preset
            if (key != null) { S.GameMic.TryGetValue(key, out wantEq); S.GameChain.TryGetValue(key, out wantChain); }
            if (!IsMicPreset(wantEq)) wantEq = null;
            if (!MicChain.IsPreset(wantChain)) wantChain = null;
            string want = wantEq == null && wantChain == null ? null : (wantEq ?? "") + "|" + (wantChain ?? "");
            if (want == S.GameMicOn) return;
            EqState st; string micName = "Mic";
            if (want != null)
            {
                string id = S.GameMicDevice;
                if (string.IsNullOrEmpty(id))
                {
                    AudioDevice d = null; try { d = MicDevice != null ? MicDevice() : null; } catch { }
                    if (d == null) { var did = CoreAudio.DefaultId(1); d = did == null ? null : CoreAudio.Devices(1).FirstOrDefault(x => x.Id == did); }
                    if (d == null) return;
                    id = d.Id; S.GameMicDevice = id; micName = "Mic: " + d.Name;
                    S.GameMicNormal = S.Eq.TryGetValue(id, out st) ? CloneEq(st) : null;
                }
                // always start from the mic's own settings, then put the game's EQ preset and/or chain on top
                EqState cur; S.Eq.TryGetValue(id, out cur);
                st = S.GameMicNormal != null ? CloneEq(S.GameMicNormal) : new EqState { Name = cur != null ? cur.Name : micName, Capture = true };
                int[] g; PBand[] pb;
                if (wantEq != null && Eq.MicPresets.TryGetValue(wantEq, out g))
                {
                    st.Param = false; st.Gains = (int[])g.Clone();
                    st.LowCut = wantEq == "Discord-ready" ? 80 : wantEq == "Flat" ? 0 : (S.GameMicNormal != null ? S.GameMicNormal.LowCut : 0);
                    st.On = wantEq != "Flat" || st.LowCut > 0;
                }
                else if (wantEq != null && PEq.MicPresets.TryGetValue(wantEq, out pb)) { st.Param = true; st.PBands = PEq.Copy(pb); st.On = true; }
                if (wantEq != null) st.Preset = wantEq;
                if (wantChain != null) st.Chain = MicChain.Make(wantChain);
                st.Capture = true; S.Eq[id] = st;
                S.GameMicOn = want;
                MicNote = (wantEq != null ? wantEq + " mic preset" : "") + (wantEq != null && wantChain != null ? " and " : "") + (wantChain != null ? "mic chain " + wantChain : "") + " on for " + (game ?? "this game") + ".";
            }
            else
            {
                if (!string.IsNullOrEmpty(S.GameMicDevice))
                {
                    if (S.GameMicNormal != null) S.Eq[S.GameMicDevice] = CloneEq(S.GameMicNormal); else S.Eq.Remove(S.GameMicDevice);
                }
                S.GameMicOn = null; S.GameMicDevice = ""; S.GameMicNormal = null;
                MicNote = "Your own mic EQ and chain are back.";
            }
            EqSwitched = true; Eq.Write(S.Eq); S.Save();
        }
        // The Games window: pick a game's mic preset (null = your normal mic EQ). If that game is running, the mic follows right away.
        public void SetGameMic(string key, string preset)
        {
            if (IsMicPreset(preset)) S.GameMic[key] = preset; else S.GameMic.Remove(key);
            if (key == CurrentGame) SwitchMic(key, CurrentGameName);
            S.Save();
        }
        public void SetGameChain(string key, string preset)
        {
            if (MicChain.IsPreset(preset)) S.GameChain[key] = preset; else S.GameChain.Remove(key);
            if (key == CurrentGame) SwitchMic(key, CurrentGameName);
            S.Save();
        }
        // Exit: your own mic EQ goes back on.
        public void EndGameMic() { if (S.GameMicOn != null) SwitchMic(null, null); }

        // ---- levels per game: when a game starts its own levels come back; when it closes, your normal ones do
        public string CurrentGame, CurrentGameName, LevelsNote;
        Preset beforeGame;
        Preset Snapshot(string name)
        {
            var p = new Preset { Name = name, ChatMix = S.ChatMix };
            foreach (var c in S.Channels) p.Volumes[c.Id] = c.Volume;
            return p;
        }
        void LoadLevels(Preset p)
        {
            foreach (var kv in p.Volumes) { var c = S.Channels.FirstOrDefault(x => x.Id == kv.Key); if (c != null) c.Volume = kv.Value; }
            S.ChatMix = p.ChatMix;
        }
        // Returns true when it switched levels (the window then reloads its faders).
        bool CheckGame()
        {
            var g = Apps.FirstOrDefault(a => a.Channel == "game" && a.Key != "system");
            string key = g == null ? null : g.Key;
            if (!Snap.On) SwitchMic(key, g == null ? null : g.Name);   // no-op unless the game's mic preset differs from what's on
            if (key == CurrentGame) return false;
            SwitchEq(key);
            if (!S.PerGame) { CurrentGame = key; CurrentGameName = g == null ? null : g.Name; S.Save(); return false; }
            if (CurrentGame == null && key != null) beforeGame = Snapshot("before");
            if (key != null)
            {
                Preset saved;
                if (S.GameLevels.TryGetValue(key, out saved)) { LoadLevels(saved); LevelsNote = "Your " + g.Name + " levels are back."; }
                else { S.GameLevels[key] = Snapshot(g.Name); LevelsNote = g.Name + " started - changes you make now are remembered for it."; }
            }
            else if (beforeGame != null) { LoadLevels(beforeGame); beforeGame = null; LevelsNote = CurrentGameName + " closed - your normal levels are back."; }
            CurrentGame = key; CurrentGameName = g == null ? null : g.Name;
            S.Save();
            return true;
        }
        // ---- v2.9 game profiles window
        public Preset CurrentLevels(string name) { return Snapshot(name); }
        // Change a game's saved levels; if that game is running, the mixer follows right away.
        public void SetGameLevels(string key, Preset p, bool save = true)
        {
            S.GameLevels[key] = p;
            if (key == CurrentGame && S.PerGame) { LoadLevels(p); Apply(); }
            if (save) S.Save();
        }
        public void ForgetGame(string key, bool levels, bool eq)
        {
            if (levels) S.GameLevels.Remove(key);
            if (eq && S.GameEq.Remove(key) && key == CurrentGame && eqNormal != null) { UseOutputEq(eqNormal); Eq.Write(S.Eq); }
            S.Save();
        }
        // The window calls this after you move a fader: while a game runs, its levels are updated.
        public void NoteLevelsChanged()
        {
            if (S.PerGame && CurrentGame != null) S.GameLevels[CurrentGame] = Snapshot(CurrentGameName);
        }
        public bool LevelsSwitched;

        // Pushes the mixer's numbers onto every app stream (only where something changed).
        public void Apply()
        {
            if (Snap.On) return;
            foreach (var a in Apps)
            {
                var c = Chan(a.Channel);
                float v = c.Volume / 100f * ChannelFactor(a.Channel) * DuckFactor(a.Channel) * AppVolume(a.Key) / 100f * BgFactor(a.Key);
                v = Math.Max(0f, Math.Min(1f, v));
                foreach (var s in a.Sessions) s.Set(v, c.Muted || AppMuted(a.Key));
                if (CoreAudio.CanRoutePerApp)
                    foreach (var pid in a.Pids)
                    {
                        string want, have;
                        if (!S.AppOutput.TryGetValue(a.Key, out want)) want = c.OutputId ?? "";
                        if (!routed.TryGetValue(pid, out have)) have = null;
                        if (have != want && !(have == null && want == ""))
                        {
                            if (CoreAudio.RouteApp(pid, want)) routed[pid] = want;
                        }
                    }
            }
        }

        // ---- v3.22 app rules: "always start at X%" and every remembered app, running or not
        readonly DateTime created = DateTime.Now;
        int refreshes;
        public string StartNote;   // "Spotify started at 40%." for the status line (the window takes it)
        // Should an app that just showed up get its start level? Yes if it appeared while Hallow Mixer was running;
        // right after Hallow Mixer starts, only if the app started with it (Windows start-up) - not one that's been playing for a while (a restart, a profile switch).
        public static bool StartFresh(int refreshCount, DateTime mixerStarted, IEnumerable<DateTime?> processStarts)
        {
            if (refreshCount > 0) return true;
            foreach (var t in processStarts) if (t.HasValue && t.Value > mixerStarted.AddSeconds(-90)) return true;
            return false;
        }
        void AppStarted(MixApp a)
        {
            if (a.Key == "system") return;
            if (S.AppStart.ContainsKey(a.Key) || S.AppChannel.ContainsKey(a.Key) || S.AppOutput.ContainsKey(a.Key) || S.BgLevel.ContainsKey(a.Key) || S.MutedApps.Contains(a.Key))
                RememberName(a.Key, a.Name);
            int lv; if (!S.AppStart.TryGetValue(a.Key, out lv)) return;
            if (!StartFresh(refreshes, created, a.Pids.Select(p => Proc.Started(p)).ToList())) return;
            lv = Math.Max(0, Math.Min(100, lv));
            if (AppVolume(a.Key) != lv) ApplyStart(a.Key, lv);
            StartNote = (a.Name ?? a.Key) + " started at " + lv + "% (its app rule).";
        }
        public void ApplyStart(string key, int level)
        {
            level = Math.Max(0, Math.Min(100, level));
            if (level == 100) S.AppVolume.Remove(key); else S.AppVolume[key] = level;
            S.Save();
        }
        public void RememberName(string key, string name)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(name) || key == "system") return;
            string old; if (S.AppNames.TryGetValue(key, out old) && old == name) return;
            if (S.AppNames.Count >= 300 && !S.AppNames.ContainsKey(key)) return;
            S.AppNames[key] = name.Length > 60 ? name.Substring(0, 60) : name;
        }
        public int? StartLevel(string key) { int v; return S.AppStart.TryGetValue(key, out v) ? (int?)v : null; }
        public void SetStartLevel(string key, int? level, string name)
        {
            if (level == null) S.AppStart.Remove(key); else { S.AppStart[key] = Math.Max(0, Math.Min(100, level.Value)); RememberName(key, name); }
            S.Save();
        }
        // "Spotify.exe", "C:\...\Spotify.exe", " spotify " -> "spotify"; null if it can't be a process name
        public static string CleanAppKey(string s)
        {
            if (s == null) return null;
            s = s.Trim().Trim('"').Trim();
            int slash = Math.Max(s.LastIndexOf('\\'), s.LastIndexOf('/')); if (slash >= 0) s = s.Substring(slash + 1);
            if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 4);
            s = s.Trim().ToLowerInvariant();
            if (s.Length == 0 || s.Length > 60 || s == "system") return null;
            foreach (var ch in s) if (!(char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-' || ch == '_' || ch == '.' || ch == '+' || ch == '(' || ch == ')')) return null;
            return s;
        }
        // every app with a choice saved, plus the ones playing now
        public List<string> RememberedApps()
        {
            return S.AppChannel.Keys.Concat(S.AppVolume.Keys).Concat(S.AppOutput.Keys).Concat(S.MutedApps).Concat(S.BgLevel.Keys).Concat(S.AppStart.Keys)
                .Concat(Apps.Select(a => a.Key)).Where(k => !string.IsNullOrEmpty(k) && k != "system").Distinct().ToList();
        }
        public bool AppRunning(string key) { return Apps.Any(x => x.Key == key); }
        public string AppName(string key)
        {
            var a = Apps.FirstOrDefault(x => x.Key == key); if (a != null && !string.IsNullOrEmpty(a.Name)) return a.Name;
            string n; if (S.AppNames.TryGetValue(key, out n) && !string.IsNullOrEmpty(n)) return n;
            return key.Length > 0 ? char.ToUpperInvariant(key[0]) + key.Substring(1) : key;
        }
        // the channel an app goes to when you haven't picked one
        public string AutoChannelOf(string key)
        {
            var a = Apps.FirstOrDefault(x => x.Key == key);
            string saved; bool had = S.AppChannel.TryGetValue(key, out saved);
            if (had) S.AppChannel.Remove(key);
            try { return Guess(key, a != null ? a.ExePath : null); } finally { if (had) S.AppChannel[key] = saved; }
        }
        // channel: null = automatic
        public void SetAppChannel(string key, string channel)
        {
            if (channel == null) S.AppChannel.Remove(key); else if (ChannelIds.Contains(channel)) S.AppChannel[key] = channel; else return;
            var a = Apps.FirstOrDefault(x => x.Key == key);
            if (a != null) { a.Channel = Guess(a.Key, a.ExePath); a.AutoChannel = channel == null; }
            S.Save(); Apply();
        }
        public void SetAppOutput(string key, string deviceId)
        {
            if (deviceId == null) S.AppOutput.Remove(key); else S.AppOutput[key] = deviceId;
            S.Save(); Apply();
        }
        public void SetAppMuted(string key, bool muted)
        {
            S.MutedApps.Remove(key); if (muted) S.MutedApps.Add(key);
            S.Save(); Apply();
        }
        public void SetBgLevel(string key, int level)   // -1 = keep playing
        {
            if (level < 0) S.BgLevel.Remove(key); else S.BgLevel[key] = Math.Max(0, Math.Min(99, level));
            S.Save(); Apply();
        }
        public void ForgetApp(string key)
        {
            S.AppChannel.Remove(key); S.AppVolume.Remove(key); S.AppOutput.Remove(key); S.MutedApps.Remove(key); S.BgLevel.Remove(key); S.AppStart.Remove(key); S.AppNames.Remove(key);
            var a = Apps.FirstOrDefault(x => x.Key == key);
            if (a != null) { a.Channel = Guess(a.Key, a.ExePath); a.AutoChannel = true; }
            S.Save(); Apply();
        }

        public void SetAppOutput(MixApp a, string deviceId)
        {
            if (deviceId == null) S.AppOutput.Remove(a.Key); else S.AppOutput[a.Key] = deviceId;
            S.Save(); Apply();
        }
        // The app that owns a process (for "volume of the app in front"). Matches by process, then by exe name.
        public MixApp AppForPid(uint pid)
        {
            var a = Apps.FirstOrDefault(x => x.Pids.Contains(pid));
            if (a != null) return a;
            var exe = Proc.Path(pid); if (exe == null) return null;
            var key = Path.GetFileNameWithoutExtension(exe).ToLowerInvariant();
            return Apps.FirstOrDefault(x => x.Key == key);
        }
        public string KeyForPid(uint pid) { var exe = Proc.Path(pid); return exe == null ? null : Path.GetFileNameWithoutExtension(exe).ToLowerInvariant(); }

        // After app choices changed from outside (an imported setup): put running apps in their channels again.
        public void ReapplyChannels()
        {
            foreach (var a in Apps) { a.Channel = Guess(a.Key, a.ExePath); a.AutoChannel = !S.AppChannel.ContainsKey(a.Key); }
            Apply();
        }
        public void MoveApp(MixApp a, string channel)
        {
            a.Channel = channel; a.AutoChannel = false;
            S.AppChannel[a.Key] = channel; S.Save(); Apply();
        }
        public void ResetApp(MixApp a)
        {
            S.AppChannel.Remove(a.Key); S.AppVolume.Remove(a.Key); S.AppOutput.Remove(a.Key); S.MutedApps.Remove(a.Key); S.BgLevel.Remove(a.Key); S.AppStart.Remove(a.Key);
            a.Channel = Guess(a.Key, a.ExePath); a.AutoChannel = true; S.Save(); Apply();
        }
        public void ApplyPreset(Preset p)
        {
            foreach (var kv in p.Volumes) { var c = S.Channels.FirstOrDefault(x => x.Id == kv.Key); if (c != null) c.Volume = kv.Value; }
            S.ChatMix = p.ChatMix; S.Save(); Apply();
        }
        public void SavePreset(Preset p)
        {
            p.Volumes.Clear(); foreach (var c in S.Channels) p.Volumes[c.Id] = c.Volume;
            p.ChatMix = S.ChatMix; S.Save();
        }
        // Uninstall: every app back to full volume and Windows' default output, including routes saved by earlier runs.
        public void ResetForUninstall()
        {
            Refresh();
            foreach (var a in Apps) { foreach (var s in a.Sessions) { s.LastSet = -1; s.Set(1f, false); } foreach (var pid in a.Pids) CoreAudio.RouteApp(pid, ""); }
            bool routedAny = S.AppOutput.Count > 0 || S.Channels.Any(c => !string.IsNullOrEmpty(c.OutputId));
            if (routedAny) CoreAudio.ClearAllRoutes();
        }
        // ---- v2.3 clean chat voices: the Chat channel plays into NVIDIA Broadcast's speaker, which cleans the voices
        // and plays them on the output you pick in the NVIDIA Broadcast app.
        public static AudioDevice BroadcastSpeaker(List<AudioDevice> outs)
        {
            return outs.FirstOrDefault(d => d.Name.IndexOf("NVIDIA Broadcast", StringComparison.OrdinalIgnoreCase) >= 0);
        }
        public void SetChatClean(bool on, string broadcastId)
        {
            var c = Chan("chat");
            if (on)
            {
                if (string.IsNullOrEmpty(broadcastId)) return;
                if (S.Split) { S.Split = false; S.SplitPrev = ""; }
                if (!S.ChatClean) S.ChatCleanPrev = c.OutputId ?? "";
                c.OutputId = broadcastId; S.ChatClean = true;
            }
            else
            {
                if (S.ChatClean) c.OutputId = S.ChatCleanPrev ?? "";
                S.ChatClean = false; S.ChatCleanPrev = "";
            }
            S.Save(); Apply();
        }
        // ---- v3.7 split headset: Chat plays on the headset's chat/voice output, so its game/chat dial (and a voice EQ) work
        public void SetSplit(bool on, AudioDevice chatOut)
        {
            var c = Chan("chat");
            if (on)
            {
                if (chatOut == null) return;
                if (S.ChatClean) { S.ChatClean = false; S.ChatCleanPrev = ""; }
                if (!S.Split) S.SplitPrev = c.OutputId ?? "";
                c.OutputId = chatOut.Id; S.Split = true;
                EqState e;
                if (!S.Eq.TryGetValue(chatOut.Id, out e) || (!e.On && e.Space == 0))
                {
                    S.Eq[chatOut.Id] = new EqState { Name = chatOut.Name, On = true, Preset = "Voice clarity", Gains = (int[])Eq.OutPresets["Voice clarity"].Clone() };
                    Eq.Write(S.Eq);
                }
            }
            else
            {
                if (S.Split) c.OutputId = S.SplitPrev ?? "";
                S.Split = false; S.SplitPrev = "";
            }
            S.Save(); Apply();
        }
        // NVIDIA Broadcast removed or its speaker gone: put Chat back so you still hear your friends. True if it did.
        public bool CheckChatClean(List<AudioDevice> outs)
        {
            if (!S.ChatClean) return false;
            var id = Chan("chat").OutputId;
            if (!string.IsNullOrEmpty(id) && outs.Any(d => d.Id == id)) return false;
            var prev = S.ChatCleanPrev ?? "";
            if (prev != "" && !outs.Any(d => d.Id == prev)) prev = "";
            Chan("chat").OutputId = prev; S.ChatClean = false; S.ChatCleanPrev = ""; S.Save(); Apply();
            return true;
        }

        // Setup "Replacing Sonar": forget per-app outputs Windows saved (Sonar's too), put running apps on the default
        // output, then re-apply only Hallow Mixer's own choices.
        public void ForgetRoutes()
        {
            Refresh();
            CoreAudio.ClearAllRoutes();
            foreach (var a in Apps) foreach (var pid in a.Pids) CoreAudio.RouteApp(pid, "");
            routed.Clear();
            Apply();
        }
        // Put every app back to full volume, unmuted, on the default output (used on exit if you choose to).
        public void ResetAllApps()
        {
            foreach (var a in Apps)
            {
                foreach (var s in a.Sessions) { s.LastSet = -1; s.Set(1f, false); }
                foreach (var pid in a.Pids) if (routed.ContainsKey(pid)) CoreAudio.RouteApp(pid, "");
            }
        }
    }
}
