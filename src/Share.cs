// Hallow Mixer - the part of your settings that makes sense on a friend's PC (for Hallow Toolkit's "Share your setup").
// Never included: device ids, outputs, OBS details, window places, saved game levels.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace HallowMixer
{
    [DataContract] public class SharedMix
    {
        [DataMember] public string From;
        [DataMember] public List<Preset> Presets;
        [DataMember] public Dictionary<string, string> AppChannel;   // app -> channel
        [DataMember] public bool DuckOnChat, DuckOnMic, PerGame, EqPerGame, LevelMedia, Overlay, MicKeep;
        [DataMember] public int DuckAmount;
        [DataMember] public string MicMode;
        [DataMember] public int PttKey;
        [DataMember] public EqState HeadsetEq;   // EQ of the headset / default output
        [DataMember] public EqState MicEq;       // EQ of the mic
    }

    public static class Share
    {
        static EqState Copy(EqState e)
        {
            if (e == null) return null;
            return new EqState { Name = e.Name, Capture = e.Capture, On = e.On, Preset = e.Preset, Gains = (e.Gains ?? new int[10]).ToArray(), LowCut = e.LowCut, Param = e.Param, Space = e.Space,
                PBands = e.PBands == null ? null : e.PBands.Select(b => b.Clone()).ToList() };
        }

        public static SharedMix Make(Settings S, string outId, string micId)
        {
            EqState h = null, m = null;
            if (outId != null) S.Eq.TryGetValue(outId, out h);
            if (micId != null) S.Eq.TryGetValue(micId, out m);
            return new SharedMix
            {
                From = "Hallow Mixer v" + Program.Version,
                Presets = S.Presets.Select(p => new Preset { Name = p.Name, ChatMix = p.ChatMix, Volumes = new Dictionary<string, int>(p.Volumes) }).ToList(),
                AppChannel = new Dictionary<string, string>(S.AppChannel),
                DuckOnChat = S.DuckOnChat, DuckOnMic = S.DuckOnMic, PerGame = S.PerGame, EqPerGame = S.EqPerGame, LevelMedia = S.LevelMedia, Overlay = S.Overlay, MicKeep = S.MicKeep,
                DuckAmount = S.DuckAmount, MicMode = S.MicMode, PttKey = S.PttKey,
                HeadsetEq = Copy(h), MicEq = Copy(m)
            };
        }

        static int Clamp(int v, int lo, int hi) { return Math.Max(lo, Math.Min(hi, v)); }
        static double Clamp(double v, double lo, double hi) { return double.IsNaN(v) ? lo : Math.Max(lo, Math.Min(hi, v)); }
        static readonly string[] Types = { "PK", "LS", "HS", "HP", "LP" };

        // Checks everything from the file (it came from someone else), then copies it in. Returns what changed.
        static EqState CleanEq(EqState e, string name, bool capture)
        {
            if (e == null) return null;
            var g = new int[10]; if (e.Gains != null) for (int i = 0; i < 10 && i < e.Gains.Length; i++) g[i] = Clamp(e.Gains[i], -15, 15);
            var r = new EqState { Name = name, Capture = capture, On = e.On, Preset = string.IsNullOrEmpty(e.Preset) ? "Custom" : e.Preset.Length > 60 ? e.Preset.Substring(0, 60) : e.Preset,
                Gains = g, LowCut = capture && Eq.LowCuts.Contains(e.LowCut) ? e.LowCut : 0, Param = e.Param, Space = capture ? 0 : Math.Max(0, Math.Min(3, e.Space)) };
            if (e.PBands != null)
                r.PBands = e.PBands.Where(b => b != null).Take(10).Select(b => new PBand {
                    Type = Types.Contains(b.Type) ? b.Type : "PK", Freq = Clamp(b.Freq, 20, 20000), Gain = Clamp(b.Gain, -15, 15), Q = Clamp(b.Q, 0.1, 10), On = b.On }).ToList();
            if (r.Param && (r.PBands == null || r.PBands.Count == 0)) r.Param = false;
            return r;
        }

        public static List<string> Apply(Engine eng, SharedMix m, AudioDevice output, AudioDevice mic)
        {
            var S = eng.S; var done = new List<string>();
            if (m.Presets != null)
            {
                int n = 0;
                foreach (var p in m.Presets.Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name)).Take(12))
                {
                    var name = p.Name.Trim(); if (name.Length > 30) name = name.Substring(0, 30);
                    var np = new Preset { Name = name, ChatMix = Clamp(p.ChatMix, -100, 100) };
                    if (p.Volumes != null) foreach (var kv in p.Volumes) if (Engine.ChannelIds.Contains(kv.Key)) np.Volumes[kv.Key] = Clamp(kv.Value, 0, 100);
                    var i = S.Presets.FindIndex(x => x.Name == name);
                    if (i >= 0) S.Presets[i] = np; else if (S.Presets.Count < 12) S.Presets.Add(np); else continue;
                    n++;
                }
                if (n > 0) done.Add(n + " preset(s)");
            }
            if (m.AppChannel != null)
            {
                int n = 0;
                foreach (var kv in m.AppChannel.Take(500))
                    if (!string.IsNullOrWhiteSpace(kv.Key) && kv.Key.Length <= 100 && Engine.ChannelIds.Contains(kv.Value)) { S.AppChannel[kv.Key.ToLowerInvariant()] = kv.Value; n++; }
                if (n > 0) { eng.ReapplyChannels(); done.Add(n + " app choice(s)"); }
            }
            S.DuckOnChat = m.DuckOnChat; S.DuckOnMic = m.DuckOnMic; S.PerGame = m.PerGame; S.EqPerGame = m.EqPerGame; S.LevelMedia = m.LevelMedia; S.Overlay = m.Overlay;
            if (m.DuckAmount > 0) S.DuckAmount = Clamp(m.DuckAmount / 5 * 5, 10, 90);
            if (m.MicMode == "off" || m.MicMode == "ptt" || m.MicMode == "ptm") S.MicMode = m.MicMode;
            if (m.PttKey > 2 && m.PttKey < 255) S.PttKey = m.PttKey;
            if (m.MicKeep && !S.MicKeep) { S.MicKeep = true; S.MicKeepLevel = -1; } else if (!m.MicKeep) S.MicKeep = false;
            done.Add("switches and mic mode");
            var h = output == null ? null : CleanEq(m.HeadsetEq, output.Name, false);
            if (h != null) { S.Eq[output.Id] = h; done.Add("headset EQ on " + output.Name); }
            var mi = mic == null ? null : CleanEq(m.MicEq, mic.Name, true);
            if (mi != null) { S.Eq[mic.Id] = mi; done.Add("mic EQ on " + mic.Name); }
            if (h != null || mi != null) Eq.Write(S.Eq);
            S.Save(); eng.Apply();
            return done;
        }

        public static string ToJson(SharedMix m)
        {
            var ser = new DataContractJsonSerializer(typeof(SharedMix), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            using (var ms = new MemoryStream()) { ser.WriteObject(ms, m); return Encoding.UTF8.GetString(ms.ToArray()); }
        }
        public static SharedMix FromJson(string json)
        {
            var ser = new DataContractJsonSerializer(typeof(SharedMix), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json))) return (SharedMix)ser.ReadObject(ms);
        }
    }
}
