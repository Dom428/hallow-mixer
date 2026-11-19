// Hallow Mixer - mic chain (v3.16): noise gate -> compressor -> limiter on the mic, through our own VST plugin
// (plugin\HallowMicChain.c, built into HallowMixer.exe) that Equalizer APO runs after the mic's EQ.
// The DLL is copied to C:\ProgramData\HallowMixer\plugins (no admin needed; the audio service can read it there).
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;

namespace HallowMixer
{
    [DataContract] public class MicChain
    {
        [DataMember] public bool On;
        [DataMember] public string Preset = "Off";
        [DataMember] public bool GateOn = true;
        [DataMember] public float GateThr = -50, GateAtk = 1, GateHold = 150, GateRel = 200, GateRange = -30;
        [DataMember] public bool CompOn = true;
        [DataMember] public float CompThr = -24, CompRatio = 3, CompAtk = 10, CompRel = 150, CompMakeup = 6, CompKnee = 6;
        [DataMember] public bool LimOn = true;
        [DataMember] public float LimCeil = -1, LimRel = 60;

        public static readonly string[] PresetNames = { "Off", "Light", "Streamer", "Loud room" };
        // Real units, in the plugin's parameter order. Light: gentle; Streamer: stronger compressor and limiter; Loud room: stronger gate.
        public static MicChain Make(string preset)
        {
            switch (preset)
            {
                case "Light": return new MicChain { On = true, Preset = preset, GateThr = -55, GateAtk = 1, GateHold = 200, GateRel = 250, GateRange = -15,
                    CompThr = -24, CompRatio = 2.5f, CompAtk = 10, CompRel = 150, CompMakeup = 4, CompKnee = 8, LimCeil = -1, LimRel = 60 };
                case "Streamer": return new MicChain { On = true, Preset = preset, GateThr = -50, GateAtk = 1, GateHold = 150, GateRel = 200, GateRange = -25,
                    CompThr = -28, CompRatio = 4, CompAtk = 5, CompRel = 120, CompMakeup = 8, CompKnee = 6, LimCeil = -1, LimRel = 50 };
                case "Loud room": return new MicChain { On = true, Preset = preset, GateThr = -40, GateAtk = 0.5f, GateHold = 120, GateRel = 150, GateRange = -40,
                    CompThr = -24, CompRatio = 3, CompAtk = 8, CompRel = 150, CompMakeup = 5, CompKnee = 6, LimCeil = -1, LimRel = 60 };
                default: return new MicChain { On = false, Preset = "Off" };
            }
        }
        public static bool IsPreset(string n) { return n != null && PresetNames.Contains(n); }

        public MicChain Copy() { var c = (MicChain)MemberwiseClone(); return c; }
        static float C(float v, float lo, float hi, float def) { return float.IsNaN(v) || float.IsInfinity(v) ? def : Math.Max(lo, Math.Min(hi, v)); }
        // Values from a shared file are checked: everything inside the plugin's ranges.
        public MicChain Clean()
        {
            var d = new MicChain();
            return new MicChain { On = On, Preset = Preset == null ? "Custom" : Preset.Length > 30 ? Preset.Substring(0, 30) : Preset,
                GateOn = GateOn, GateThr = C(GateThr, -80, -10, d.GateThr), GateAtk = C(GateAtk, 0.1f, 50, d.GateAtk), GateHold = C(GateHold, 0, 500, d.GateHold),
                GateRel = C(GateRel, 5, 2000, d.GateRel), GateRange = C(GateRange, -90, 0, d.GateRange),
                CompOn = CompOn, CompThr = C(CompThr, -60, 0, d.CompThr), CompRatio = C(CompRatio, 1, 20, d.CompRatio), CompAtk = C(CompAtk, 0.1f, 200, d.CompAtk),
                CompRel = C(CompRel, 5, 2000, d.CompRel), CompMakeup = C(CompMakeup, 0, 24, d.CompMakeup), CompKnee = C(CompKnee, 0, 24, d.CompKnee),
                LimOn = LimOn, LimCeil = C(LimCeil, -24, 0, d.LimCeil), LimRel = C(LimRel, 5, 1000, d.LimRel) };
        }

        // The plugin's chunk: "HMC1", version 1, 16 floats.
        public byte[] Chunk()
        {
            var c = Clean();
            var v = new float[] { c.GateOn ? 1 : 0, c.GateThr, c.GateAtk, c.GateHold, c.GateRel, c.GateRange,
                c.CompOn ? 1 : 0, c.CompThr, c.CompRatio, c.CompAtk, c.CompRel, c.CompMakeup, c.CompKnee,
                c.LimOn ? 1 : 0, c.LimCeil, c.LimRel };
            var b = new byte[12 + v.Length * 4];
            BitConverter.GetBytes(0x31434D48).CopyTo(b, 0); BitConverter.GetBytes(1).CopyTo(b, 4); BitConverter.GetBytes(v.Length).CopyTo(b, 8);
            for (int i = 0; i < v.Length; i++) BitConverter.GetBytes(v[i]).CopyTo(b, 12 + i * 4);
            return b;
        }

        // The Equalizer APO line (null if the plugin couldn't be put in place).
        public string ConfigLine()
        {
            var lib = Plugin.Ensure(); if (lib == null) return null;
            // the same shape Equalizer APO's own Editor writes: Library <path> ChunkData "<base64>" (path quoted only if it has spaces)
            return "VSTPlugin: Library " + (lib.Contains(" ") ? "\"" + lib + "\"" : lib) + " ChunkData \"" + Convert.ToBase64String(Chunk()) + "\"";
        }
    }

    public static class Plugin
    {
        public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"HallowMixer\plugins"); } }
        static string done;
        public static byte[] Bytes()
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("HallowMixer.HallowMicChain.dll"))
            {
                if (s == null) return null;
                var m = new MemoryStream(); s.CopyTo(m); return m.ToArray();
            }
        }
        // Copies the plugin next to our Equalizer APO file. The name carries a hash of the DLL, so a newer build never has to
        // replace a file the audio service still has loaded.
        public static string Ensure()
        {
            if (done != null && File.Exists(done)) return done;
            if (Snap.On) return @"C:\ProgramData\HallowMixer\plugins\HallowMicChain.dll";
            try
            {
                var b = Bytes(); if (b == null) return null;
                string hash; using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(b), 0, 4).Replace("-", "").ToLowerInvariant();
                var dir = Dir; bool fresh = !Directory.Exists(dir);
                Directory.CreateDirectory(dir);
                if (fresh) Lock(dir);
                var path = Path.Combine(dir, "HallowMicChain-" + hash + ".dll");
                if (!File.Exists(path) || new FileInfo(path).Length != b.Length)
                {
                    var tmp = path + ".tmp"; File.WriteAllBytes(tmp, b);
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(tmp, path);
                }
                done = path; return path;
            }
            catch { return null; }
        }
        // Only this Windows account (and the system) may change the plugin; everyone - the audio service included - may read and run it.
        static void Lock(string dir)
        {
            try
            {
                var ds = new DirectorySecurity();
                ds.SetAccessRuleProtection(true, false);
                var inh = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
                Action<IdentityReference, FileSystemRights> add = (who, r) => ds.AddAccessRule(new FileSystemAccessRule(who, r, inh, PropagationFlags.None, AccessControlType.Allow));
                add(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl);
                add(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl);
                add(WindowsIdentity.GetCurrent().User, FileSystemRights.FullControl);
                add(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute);
                add(new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null), FileSystemRights.ReadAndExecute);
                Directory.SetAccessControl(dir, ds);
            }
            catch { }
        }
    }
}
