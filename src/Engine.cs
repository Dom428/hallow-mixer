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
            PrimaryOut = ""; FallbackOut = ""; MiniLeft = -1; MiniTop = -1;
            Eq = new Dictionary<string, EqState>(); AppOutput = new Dictionary<string, string>();
            AutoUpdate = true; LastUpdateCheck = ""; MicKeepLevel = -1; ApiOn = true; NowPlayingOn = true; SmartDuck = true; PresetKeys = true; MutedWarn = true; QuietFrom = 23; QuietTo = 7; QuietMax = 40; BgLevel = new Dictionary<string, int>(); SetupMode = ""; ChatCleanPrev = ""; ObsHost = "localhost"; ObsPort = 4455; ObsPass = ""; EqPerGame = true; GameEq = new Dictionary<string, Dictionary<string, EqState>>(); MicMode = "off"; PttKey = 0x05; WinLeft = -1; WinTop = -1; WinWidth = -1; WinHeight = -1;
            MutedApps = new List<string>(); GameLevels = new Dictionary<string, Preset>();
            AppChannel = new Dictionary<string, string>(); AppVolume = new Dictionary<string, int>();
        }

        static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HallowMixer"); } }
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
            if (s.GameLevels == null) s.GameLevels = new Dictionary<string, Preset>();
            if (s.GameEq == null) s.GameEq = new Dictionary<string, Dictionary<string, EqState>>();
            if (s.StreamLinks == null) s.StreamLinks = new Dictionary<string, List<string>>();
            if (s.StreamLevel == null) s.StreamLevel = new Dictionary<string, int>();
            if (s.StreamOff == null) s.StreamOff = new List<string>();
            if (s.ScenePresets == null) s.ScenePresets = new Dictionary<string, string>();
            if (s.SetupMode == null) s.SetupMode = "";
            if (s.BgLevel == null) s.BgLevel = new Dictionary<string, int>();
            if (s.ChatCleanPrev == null) s.ChatCleanPrev = "";
            if (string.IsNullOrEmpty(s.ObsHost)) s.ObsHost = "localhost";
            if (s.ObsPort <= 0) s.ObsPort = 4455;
            if (s.DuckAmount <= 0 || s.DuckAmount > 100) s.DuckAmount = 50;
            foreach (var c in Engine.ChannelIds) if (!s.Channels.Any(x => x.Id == c)) s.Channels.Add(new ChannelState { Id = c });
            return s;
        }
        public void Save()
        {
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

        static string Friendly(string exe, string key)
        {
            try
            {
                if (exe != null)
                {
                    var fi = FileVersionInfo.GetVersionInfo(exe);
                    var n = !string.IsNullOrWhiteSpace(fi.FileDescription) ? fi.FileDescription : fi.ProductName;
                    if (!string.IsNullOrWhiteSpace(n) && n.Length < 40) return n.Trim();
                }
            }
            catch { }
            return key.Length > 0 ? char.ToUpper(key[0]) + key.Substring(1) : key;
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
                    key = exe != null ? Path.GetFileNameWithoutExtension(exe).ToLowerInvariant() : ("pid" + s.Pid);
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
            if (ch == "media") f *= LevelMediaGain;
            return f;
        }

        // ---- loudness levelling for Media: loud moments (ads, explosions) go down, quiet ones come up a little.
        // Windows reports each app's level before its volume is applied, so this always sees the real loudness.
        public float LevelMediaGain = 1f; float mediaEnv;
        public bool UpdateLevel(float mediaPeak)
        {
            float old = LevelMediaGain;
            if (!S.LevelMedia) { mediaEnv = 0; LevelMediaGain = 1f; return Math.Abs(old - 1f) > 0.004f; }
            mediaEnv += (mediaPeak - mediaEnv) * (mediaPeak > mediaEnv ? 0.7f : 0.015f);   // follows loud parts fast, lets go slowly
            if (mediaEnv > 0.02f)   // only while something is playing (silence keeps the last setting)
            {
                float target = Math.Max(0.3f, Math.Min(1.4f, 0.5f / mediaEnv));
                LevelMediaGain += (target - LevelMediaGain) * (target < LevelMediaGain ? 0.55f : 0.02f);
            }
            return Math.Abs(old - LevelMediaGain) > 0.004f;
        }

        // ---- EQ per game: a game's headset/speaker EQ comes back when it starts; your normal EQ when it closes. Mic EQ is never touched.
        Dictionary<string, EqState> eqNormal;
        public bool EqSwitched;
        static EqState CloneEq(EqState e) { return new EqState { Name = e.Name, Capture = e.Capture, On = e.On, Preset = e.Preset, LowCut = e.LowCut, Space = e.Space, Gains = e.Gains == null ? new int[10] : (int[])e.Gains.Clone(), Param = e.Param, PBands = e.PBands == null ? null : PEq.Copy(e.PBands) }; }
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
            S.AppChannel.Remove(a.Key); S.AppVolume.Remove(a.Key); S.AppOutput.Remove(a.Key); S.MutedApps.Remove(a.Key); S.BgLevel.Remove(a.Key);
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
