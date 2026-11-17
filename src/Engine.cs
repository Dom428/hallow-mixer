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

        // The JSON reader skips field initialisers, so set the defaults here (values in the file then override them).
        [OnDeserializing] void Defaults(StreamingContext c)
        {
            DuckOnChat = true; DuckAmount = 50; PerGame = true; Overlay = true;
            PrimaryOut = ""; FallbackOut = ""; MiniLeft = -1; MiniTop = -1;
            Eq = new Dictionary<string, EqState>();
            MutedApps = new List<string>(); GameLevels = new Dictionary<string, Preset>();
            AppChannel = new Dictionary<string, string>(); AppVolume = new Dictionary<string, int>();
        }

        static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HallowMixer"); } }
        static string FilePath { get { return Path.Combine(Dir, "settings.json"); } }
        public static Settings Load()
        {
            Settings s = null;
            try { if (File.Exists(FilePath)) using (var f = File.OpenRead(FilePath)) s = (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(f); } catch { }
            if (s == null) s = new Settings();
            if (s.Channels == null) s.Channels = new List<ChannelState>();
            if (s.AppChannel == null) s.AppChannel = new Dictionary<string, string>();
            if (s.AppVolume == null) s.AppVolume = new Dictionary<string, int>();
            if (s.Presets == null || s.Presets.Count == 0) s.Presets = DefaultPresets();
            if (s.MutedApps == null) s.MutedApps = new List<string>();
            if (s.Eq == null) s.Eq = new Dictionary<string, EqState>();
            if (s.GameLevels == null) s.GameLevels = new Dictionary<string, Preset>();
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
        public void ToggleAppMute(MixApp a)
        {
            if (!S.MutedApps.Remove(a.Key)) S.MutedApps.Add(a.Key);
            S.Save(); Apply();
        }

        // ---- auto-ducking: someone talks in Chat (or you talk) -> other channels go down, then fade back
        public float DuckChat = 1f, DuckMic = 1f;          // current multipliers (1 = not ducked)
        DateTime chatHold, micHold;
        public bool Ducking { get { return DuckChat < 0.97f || DuckMic < 0.97f; } }
        // Called ~20 times a second with the live levels. Returns true when the volumes need re-applying.
        public bool UpdateDuck(float chatPeak, float micPeak)
        {
            var now = DateTime.UtcNow;
            if (chatPeak > 0.03f) chatHold = now.AddMilliseconds(700);
            if (micPeak > 0.08f) micHold = now.AddMilliseconds(700);
            float low = 1f - S.DuckAmount / 100f;
            float tChat = S.DuckOnChat && now < chatHold ? low : 1f;
            float tMic = S.DuckOnMic && now < micHold ? low : 1f;
            float oc = DuckChat, om = DuckMic;
            DuckChat += (tChat - DuckChat) * (tChat < DuckChat ? 0.45f : 0.07f);   // down fast, back up gently
            DuckMic += (tMic - DuckMic) * (tMic < DuckMic ? 0.45f : 0.07f);
            if (Math.Abs(DuckChat - tChat) < 0.005f) DuckChat = tChat;
            if (Math.Abs(DuckMic - tMic) < 0.005f) DuckMic = tMic;
            return Math.Abs(oc - DuckChat) > 0.004f || Math.Abs(om - DuckMic) > 0.004f;
        }
        float DuckFactor(string ch)
        {
            float f = 1f;
            if (ch == "game" || ch == "music" || ch == "media") f *= DuckChat;
            if (ch == "music" || ch == "media") f *= DuckMic;
            return f;
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
            if (!S.PerGame) { CurrentGame = key; CurrentGameName = g == null ? null : g.Name; return false; }
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
                float v = c.Volume / 100f * ChannelFactor(a.Channel) * DuckFactor(a.Channel) * AppVolume(a.Key) / 100f;
                v = Math.Max(0f, Math.Min(1f, v));
                foreach (var s in a.Sessions) s.Set(v, c.Muted || AppMuted(a.Key));
                if (CoreAudio.CanRoutePerApp)
                    foreach (var pid in a.Pids)
                    {
                        string want = c.OutputId ?? "", have;
                        if (!routed.TryGetValue(pid, out have)) have = null;
                        if (have != want && !(have == null && want == ""))
                        {
                            if (CoreAudio.RouteApp(pid, want)) routed[pid] = want;
                        }
                    }
            }
        }

        public void MoveApp(MixApp a, string channel)
        {
            a.Channel = channel; a.AutoChannel = false;
            S.AppChannel[a.Key] = channel; S.Save(); Apply();
        }
        public void ResetApp(MixApp a)
        {
            S.AppChannel.Remove(a.Key); S.AppVolume.Remove(a.Key);
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
