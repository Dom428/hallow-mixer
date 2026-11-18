// Hallow Mixer - Windows Core Audio interop (sessions, endpoint volume, meters, default devices, per-app output).
// Written for the C# 5 compiler that ships with .NET Framework 4.8, so it builds on any Windows 10/11 PC.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace HallowMixer
{
    [StructLayout(LayoutKind.Sequential)] struct PKEY { public Guid fmtid; public int pid; public PKEY(Guid g, int p) { fmtid = g; pid = p; } }
    [StructLayout(LayoutKind.Sequential)] struct PROPVARIANT { public ushort vt; public ushort r1, r2, r3; public IntPtr p; public IntPtr p2; }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, int mask, out IMMDeviceCollection list);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice dev);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice dev);
    }
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection { [PreserveSig] int GetCount(out uint n); [PreserveSig] int Item(uint i, out IMMDevice dev); }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore ps);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }
    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int n);
        [PreserveSig] int GetAt(int i, out PKEY k);
        [PreserveSig] int GetValue(ref PKEY k, out PROPVARIANT v);
    }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr n);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr n);
        [PreserveSig] int GetChannelCount(out uint n);
        [PreserveSig] int SetMasterVolumeLevel(float db, ref Guid ctx);
        [PreserveSig] int SetMasterVolumeLevelScalar(float v, ref Guid ctx);
        [PreserveSig] int GetMasterVolumeLevel(out float db);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float v);
        [PreserveSig] int SetChannelVolumeLevel(uint c, float db, ref Guid ctx);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint c, float v, ref Guid ctx);
        [PreserveSig] int GetChannelVolumeLevel(uint c, out float db);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint c, out float v);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool m, ref Guid ctx);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool m);
        [PreserveSig] int GetVolumeStepInfo(out uint step, out uint count);
        [PreserveSig] int VolumeStepUp(ref Guid ctx);
        [PreserveSig] int VolumeStepDown(ref Guid ctx);
        [PreserveSig] int QueryHardwareSupport(out uint mask);
        [PreserveSig] int GetVolumeRange(out float minDb, out float maxDb, out float incDb);
    }
    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioMeterInformation { [PreserveSig] int GetPeakValue(out float v); }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr guid, int flags, out IntPtr ctl);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr guid, int flags, out IntPtr vol);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator e);
    }
    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator { [PreserveSig] int GetCount(out int n); [PreserveSig] int GetSession(int i, out IAudioSessionControl2 s); }
    [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid ctx);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid ctx);
        [PreserveSig] int GetGroupingParam(out Guid g);
        [PreserveSig] int SetGroupingParam(ref Guid g, ref Guid ctx);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr n);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr n);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetProcessId(out uint pid);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }
    // Per-channel volume of one app stream. (Mute = all channels at 0.)
    [ComImport, Guid("1C158861-B533-4B30-B1CF-E853E51C59B8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IChannelAudioVolume
    {
        [PreserveSig] int GetChannelCount(out uint n);
        [PreserveSig] int SetChannelVolume(uint i, float v, ref Guid ctx);
        [PreserveSig] int GetChannelVolume(uint i, out float v);
        [PreserveSig] int SetAllVolumes(uint n, [MarshalAs(UnmanagedType.LPArray)] float[] v, ref Guid ctx);
        [PreserveSig] int GetAllVolumes(uint n, [MarshalAs(UnmanagedType.LPArray)] float[] v);
    }
    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr f);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, int def, out IntPtr f);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr f, IntPtr m);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, int def, out long a, out long b);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr p);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr m);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr m);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, int fx, ref PKEY k, out PROPVARIANT v);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, int fx, ref PKEY k, ref PROPVARIANT v);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
    }
    // Per-app output device (the same Windows feature as Settings > Sound > Volume mixer > per-app output). Windows 10 21H2+ / 11.
    [Guid("ab3d4648-e242-459f-b02f-541c70306324"), InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    interface IAudioPolicyConfigFactory
    {
        int _1(); int _2(); int _3(); int _4(); int _5(); int _6(); int _7(); int _8(); int _9(); int _10();
        int _11(); int _12(); int _13(); int _14(); int _15(); int _16(); int _17(); int _18(); int _19();
        [PreserveSig] int SetPersistedDefaultAudioEndpoint(uint processId, int flow, int role, IntPtr deviceId);
        [PreserveSig] int GetPersistedDefaultAudioEndpoint(uint processId, int flow, int role, out IntPtr deviceId);
        [PreserveSig] int ClearAllPersistedApplicationDefaultEndpoints();
    }

    public class AudioDevice { public string Id; public string Name; public bool IsDefault; public override string ToString() { return Name; } }

    // One app's audio stream. A process can have several (one per device), they are grouped by process in the Engine.
    public class AppSession
    {
        internal IAudioSessionControl2 Ctl;
        internal IChannelAudioVolume Vol;
        internal IAudioMeterInformation Meter;
        public uint Pid;
        public string DeviceId;
        public bool IsSystem;
        public float LastSet = -1;
        public bool LastMute;
        public float Peak()
        {
            float v = 0;
            try { if (Meter != null) Meter.GetPeakValue(out v); } catch { }
            return v;
        }
        // Windows' current volume of this stream (first speaker channel), 0..1
        public float Current() { float v = -1; try { Vol.GetChannelVolume(0, out v); } catch { } return v; }
        public bool Expired()
        {
            int s = 2;
            try { Ctl.GetState(out s); } catch { }
            return s == 2;
        }
        public void Set(float v, bool mute)
        { if (Snap.On) return;
            Guid g = Guid.Empty;
            float target = mute ? 0f : v;
            if (Math.Abs(target - LastSet) <= 0.004f && LastSet >= 0) return;
            try
            {
                uint n; Vol.GetChannelCount(out n); if (n == 0 || n > 32) return;
                var a = new float[n]; for (int i = 0; i < n; i++) a[i] = target;
                if (Vol.SetAllVolumes(n, a, ref g) >= 0) { LastSet = target; LastMute = mute; }
            }
            catch { }
        }
    }

    public static class CoreAudio
    {
        public const int Render = 0, Capture = 1;
        static readonly PKEY FriendlyName = new PKEY(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
        static Guid IID_Volume = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
        static Guid IID_Meter = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");
        static Guid IID_Sessions = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PROPVARIANT pv);

        static object Create(string clsid) { return Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid(clsid))); }
        static IMMDeviceEnumerator Enum() { return (IMMDeviceEnumerator)Create("BCDE0395-E52F-467C-8E3D-C4579291692E"); }

        static string Name(IMMDevice d)
        {
            IPropertyStore ps; if (d.OpenPropertyStore(0, out ps) < 0) return null;
            PKEY k = FriendlyName; PROPVARIANT v;
            if (ps.GetValue(ref k, out v) < 0) return null;
            string s = v.vt == 31 ? Marshal.PtrToStringUni(v.p) : null;
            PropVariantClear(ref v); return s;
        }
        static T Activate<T>(IMMDevice d, Guid iid) where T : class
        {
            object o; if (d.Activate(ref iid, 23, IntPtr.Zero, out o) < 0) return null; return o as T;
        }
        static IMMDevice Device(string id) { IMMDevice d; return Enum().GetDevice(id, out d) < 0 ? null : d; }

        public static string DefaultId(int flow)
        {
            IMMDevice d; if (Enum().GetDefaultAudioEndpoint(flow, 0, out d) < 0 || d == null) return null;
            string id; d.GetId(out id); return id;
        }
        public static List<AudioDevice> Devices(int flow)
        {
            var res = new List<AudioDevice>(); string def = DefaultId(flow);
            IMMDeviceCollection col; if (Enum().EnumAudioEndpoints(flow, 1, out col) < 0) return res;
            uint n; col.GetCount(out n);
            for (uint i = 0; i < n; i++)
            {
                IMMDevice d; if (col.Item(i, out d) < 0) continue;
                var a = new AudioDevice(); d.GetId(out a.Id); a.Name = Name(d) ?? a.Id; a.IsDefault = a.Id == def; res.Add(a);
            }
            return res;
        }
        public static void SetDefault(string id)
        {
            if (Snap.On) return;
            var pc = (IPolicyConfig)Create("870af99c-171d-4f9e-af0d-e63df40c2bc9");
            for (int role = 0; role < 3; role++) pc.SetDefaultEndpoint(id, role);
        }

        // ---- device (endpoint) volume, mute and meter: used for the master output and the mic
        public static float GetLevel(string id)
        {
            var d = Device(id); if (d == null) return -1;
            var v = Activate<IAudioEndpointVolume>(d, IID_Volume); if (v == null) return -1;
            float f; v.GetMasterVolumeLevelScalar(out f); return f;
        }
        public static void SetLevel(string id, float level)
        {
            if (Snap.On) return;
            var d = Device(id); if (d == null) return;
            var v = Activate<IAudioEndpointVolume>(d, IID_Volume); if (v == null) return;
            Guid g = Guid.Empty; v.SetMasterVolumeLevelScalar(Math.Max(0f, Math.Min(1f, level)), ref g);
        }
        // level in dB and the device's range (for auto mic level)
        public static bool GetDb(string id, out float db, out float minDb, out float maxDb)
        {
            db = minDb = maxDb = 0; float inc;
            var d = Device(id); if (d == null) return false;
            var v = Activate<IAudioEndpointVolume>(d, IID_Volume); if (v == null) return false;
            return v.GetMasterVolumeLevel(out db) >= 0 && v.GetVolumeRange(out minDb, out maxDb, out inc) >= 0;
        }
        public static void SetDb(string id, float db)
        {
            if (Snap.On) return;
            var d = Device(id); if (d == null) return;
            var v = Activate<IAudioEndpointVolume>(d, IID_Volume); if (v == null) return;
            Guid g = Guid.Empty; v.SetMasterVolumeLevel(db, ref g);
        }
        public static bool GetMute(string id)
        {
            var d = Device(id); if (d == null) return false;
            var v = Activate<IAudioEndpointVolume>(d, IID_Volume); if (v == null) return false;
            bool m; v.GetMute(out m); return m;
        }
        public static void SetMute(string id, bool mute)
        {
            if (Snap.On) return;
            var d = Device(id); if (d == null) return;
            var v = Activate<IAudioEndpointVolume>(d, IID_Volume); if (v == null) return;
            Guid g = Guid.Empty; v.SetMute(mute, ref g);
        }
        static readonly Dictionary<string, IAudioMeterInformation> meters = new Dictionary<string, IAudioMeterInformation>();
        public static float DevicePeak(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            IAudioMeterInformation m;
            if (!meters.TryGetValue(id, out m))
            {
                var d = Device(id); m = d == null ? null : Activate<IAudioMeterInformation>(d, IID_Meter);
                meters[id] = m;
            }
            float v = 0; try { if (m != null) m.GetPeakValue(out v); } catch { meters.Remove(id); }
            return v;
        }

        // ---- app sessions on every active output device (so apps playing through Sonar's virtual devices are seen too)
        public static List<AppSession> Sessions()
        {
            var res = new List<AppSession>();
            IMMDeviceCollection col; if (Enum().EnumAudioEndpoints(Render, 1, out col) < 0) return res;
            uint n; col.GetCount(out n);
            for (uint i = 0; i < n; i++)
            {
                IMMDevice d; if (col.Item(i, out d) < 0) continue;
                string devId; d.GetId(out devId);
                var mgr = Activate<IAudioSessionManager2>(d, IID_Sessions); if (mgr == null) continue;
                IAudioSessionEnumerator en; if (mgr.GetSessionEnumerator(out en) < 0 || en == null) continue;
                int count; en.GetCount(out count);
                for (int j = 0; j < count; j++)
                {
                    IAudioSessionControl2 c; if (en.GetSession(j, out c) < 0 || c == null) continue;
                    int state; c.GetState(out state); if (state == 2) continue;
                    var s = new AppSession { Ctl = c, DeviceId = devId };
                    c.GetProcessId(out s.Pid);
                    s.IsSystem = c.IsSystemSoundsSession() == 0;
                    s.Vol = c as IChannelAudioVolume; s.Meter = c as IAudioMeterInformation;
                    if (s.Vol == null) continue;
                    res.Add(s);
                }
            }
            return res;
        }

        // ---- per-app output device
        [DllImport("combase.dll", PreserveSig = false)] static extern void RoGetActivationFactory(IntPtr classId, [In] ref Guid iid, [MarshalAs(UnmanagedType.IInspectable)] out object factory);
        [DllImport("combase.dll", PreserveSig = false)] static extern void WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string src, int length, out IntPtr hstring);
        [DllImport("combase.dll", PreserveSig = false)] static extern void WindowsDeleteString(IntPtr hstring);
        static IAudioPolicyConfigFactory policy; static bool policyTried;
        static IAudioPolicyConfigFactory Policy()
        {
            if (policyTried) return policy;
            policyTried = true;
            try
            {
                IntPtr cls; const string name = "Windows.Media.Internal.AudioPolicyConfig";
                WindowsCreateString(name, name.Length, out cls);
                Guid iid = typeof(IAudioPolicyConfigFactory).GUID; object f;
                RoGetActivationFactory(cls, ref iid, out f);
                WindowsDeleteString(cls);
                policy = f as IAudioPolicyConfigFactory;
            }
            catch { policy = null; }
            return policy;
        }
        public static bool CanRoutePerApp { get { return Policy() != null; } }
        // Forget every per-app output Windows has saved (Settings > Sound > Volume mixer > app output).
        public static void ClearAllRoutes() { var p = Policy(); if (p != null) try { p.ClearAllPersistedApplicationDefaultEndpoints(); } catch { } }
        // deviceId "" = back to the Windows default output
        public static bool RouteApp(uint pid, string deviceId)
        { if (Snap.On) return false;
            var p = Policy(); if (p == null || pid == 0) return false;
            IntPtr h = IntPtr.Zero;
            try
            {
                if (!string.IsNullOrEmpty(deviceId))
                {
                    string full = @"\\?\SWD#MMDEVAPI#" + deviceId + "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
                    WindowsCreateString(full, full.Length, out h);
                }
                int a = p.SetPersistedDefaultAudioEndpoint(pid, Render, 0, h);
                int b = p.SetPersistedDefaultAudioEndpoint(pid, Render, 1, h);
                return a >= 0 && b >= 0;
            }
            catch { return false; }
            finally { if (h != IntPtr.Zero) WindowsDeleteString(h); }
        }
    }

    // Full exe path of any process, even ones we can't open normally (games with anti-cheat).
    public static class Proc
    {
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(int access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder sb, ref int size);
        public static string Path(uint pid)
        {
            IntPtr h = OpenProcess(0x1000, false, pid); if (h == IntPtr.Zero) return null;
            try { var sb = new StringBuilder(1024); int n = sb.Capacity; return QueryFullProcessImageName(h, 0, sb, ref n) ? sb.ToString() : null; }
            finally { CloseHandle(h); }
        }
    }
}
