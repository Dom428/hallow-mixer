// Hallow Mixer - v3.18 sidetone: hear your own mic in your headset, with as little delay as Windows' shared mode allows.
// The mic is captured event-driven (one engine period, ~10 ms) and written straight into a small render buffer on the
// headset; the queue is kept short (old audio is dropped rather than letting the delay grow). The raw mic is asked for
// (Windows' raw mode skips effects such as Equalizer APO where the mic allows it). Not Windows' "Listen to this device".
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace HallowMixer
{
    [ComImport, Guid("726778CD-F60A-4eda-82DE-E47610CD78AA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioClient2
    {
        [PreserveSig] int Initialize(int share, uint flags, long buf, long period, IntPtr fmt, IntPtr session);
        [PreserveSig] int GetBufferSize(out uint n);
        [PreserveSig] int GetStreamLatency(out long l);
        [PreserveSig] int GetCurrentPadding(out uint n);
        [PreserveSig] int IsFormatSupported(int share, IntPtr fmt, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr fmt);
        [PreserveSig] int GetDevicePeriod(out long def, out long min);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr h);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object o);
        [PreserveSig] int IsOffloadCapable(int category, out int capable);
        [PreserveSig] int SetClientProperties(ref AudioClientProps p);
        [PreserveSig] int GetBufferSizeLimits(IntPtr fmt, int eventDriven, out long min, out long max);
    }
    [StructLayout(LayoutKind.Sequential)] struct AudioClientProps { public uint Size; public int Offload; public int Category; public int Options; }
    [ComImport, Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioRenderClient
    {
        [PreserveSig] int GetBuffer(uint frames, out IntPtr data);
        [PreserveSig] int ReleaseBuffer(uint frames, uint flags);
    }

    public class Sidetone
    {
        public string MicId, OutId;             // what it's running on
        public volatile float Gain;             // 0..1 (already curved)
        public volatile bool Silent;            // mic muted / push-to-talk up: nothing plays
        public string Error;                    // why it stopped (null while fine)
        public bool Raw;                        // Windows accepted raw mode for the mic
        // measured
        public long Captured, Rendered, Dropped, Underruns;
        public double CapturePeriodMs, RenderLatencyMs, QueueMs;
        public double LatencyMs { get { return CapturePeriodMs + QueueMs + RenderLatencyMs; } }

        Thread thread; volatile bool running;
        static readonly Guid FloatSub = new Guid("00000003-0000-0010-8000-00aa00389b71");

        // --sidetone-test <seconds>: developer check of the pipeline at -120 dB (nothing audible), on a headset that isn't
        // Windows' default if there is one. Prints what it measured; exit code 0 = ran and kept the delay under 30 ms.
        public static int Test(int seconds)
        {
            var mic = CoreAudio.DefaultId(1);
            var outs = CoreAudio.Devices(0);
            var other = outs.Find(d => !d.IsDefault); var def = outs.Find(d => d.IsDefault);
            var target = other ?? def;
            if (mic == null || target == null) { FamilyPipe.Print("no mic or output"); return 2; }
            var s = new Sidetone();
            s.Start(mic, target.Id, 1e-6f);   // -120 dB: inaudible, but the copy path runs
            Thread.Sleep(seconds * 1000);
            var running = s.Running; s.Stop();
            FamilyPipe.Print("output: " + (other != null ? "non-default device" : "default device") + ", level -120 dB (inaudible)");
            FamilyPipe.Print("error: " + (s.Error ?? "none") + ", raw mic: " + s.Raw + ", was running: " + running);
            FamilyPipe.Print(string.Format("captured {0} frames, rendered {1}, dropped {2}, underruns {3}", s.Captured, s.Rendered, s.Dropped, s.Underruns));
            FamilyPipe.Print(string.Format("capture period {0:0.0} ms + queue {1:0.0} ms + output latency {2:0.0} ms = {3:0.0} ms", s.CapturePeriodMs, s.QueueMs, s.RenderLatencyMs, s.LatencyMs));
            return s.Error == null && s.Captured > 0 && s.Rendered > 0 && s.LatencyMs < 30 ? 0 : 1;
        }

        public static float Curve(int percent) { var p = Math.Max(0, Math.Min(100, percent)) / 100f; return p * p; }

        public void Start(string micId, string outId, float gain)
        {
            Stop();
            MicId = micId; OutId = outId; Gain = gain; Error = null; Captured = Rendered = Dropped = Underruns = 0;
            running = true;
            thread = new Thread(Run) { IsBackground = true, Name = "HallowMixer sidetone", Priority = ThreadPriority.Highest };
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }
        public void Stop()
        {
            running = false;
            var t = thread; thread = null;
            if (t != null && t != Thread.CurrentThread) t.Join(1500);
        }
        public bool Running { get { return running && thread != null && Error == null; } }

        [DllImport("avrt.dll", CharSet = CharSet.Unicode)] static extern IntPtr AvSetMmThreadCharacteristics(string task, ref int index);
        [DllImport("avrt.dll")] static extern bool AvRevertMmThreadCharacteristics(IntPtr h);

        static IMMDevice Device(string id)
        {
            var en = (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
            IMMDevice dev; if (en.GetDevice(id, out dev) < 0) return null; return dev;
        }

        void Run()
        {
            IntPtr capFmt = IntPtr.Zero, outMix = IntPtr.Zero, outFmt = IntPtr.Zero, mmcss = IntPtr.Zero;
            IAudioClient2 capC = null, outC = null;
            var ev = new AutoResetEvent(false);
            try
            {
                int task = 0; try { mmcss = AvSetMmThreadCharacteristics("Pro Audio", ref task); } catch { }
                var md = Device(MicId); var od = Device(OutId);
                if (md == null || od == null) { Error = "The mic or the headset isn't connected."; return; }
                Guid iid = typeof(IAudioClient2).GUID; object o;
                if (md.Activate(ref iid, 23, IntPtr.Zero, out o) < 0) { Error = "Couldn't open the mic."; return; }
                capC = (IAudioClient2)o;
                if (od.Activate(ref iid, 23, IntPtr.Zero, out o) < 0) { Error = "Couldn't open the headset."; return; }
                outC = (IAudioClient2)o;

                // the raw mic where Windows allows it (skips effects like Equalizer APO)
                var props = new AudioClientProps { Size = 16, Category = 0, Options = 1 };
                Raw = capC.SetClientProperties(ref props) >= 0;

                capC.GetMixFormat(out capFmt);
                int tag = Marshal.ReadInt16(capFmt, 0), ch = Marshal.ReadInt16(capFmt, 2), rate = Marshal.ReadInt32(capFmt, 4);
                int block = Marshal.ReadInt16(capFmt, 12), bits = Marshal.ReadInt16(capFmt, 14);
                bool isFloat = tag == 3;
                if (tag == unchecked((short)0xFFFE)) { var sub = new byte[16]; Marshal.Copy(capFmt + 24, sub, 0, 16); isFloat = new Guid(sub) == FloatSub; }
                long def, min; capC.GetDevicePeriod(out def, out min);
                int hr = capC.Initialize(0, 0x40000, 0, 0, capFmt, IntPtr.Zero);   // shared, event-driven, one engine period
                if (hr < 0) { Error = "Couldn't start the mic (0x" + hr.ToString("X8") + ")."; return; }
                capC.SetEventHandle(ev.SafeWaitHandle.DangerousGetHandle());
                CapturePeriodMs = def / 10000.0;

                // headset: float at the mic's rate and the headset's channel layout; Windows converts the rate
                outC.GetMixFormat(out outMix);
                int och = Marshal.ReadInt16(outMix, 2); int mask = Marshal.ReadInt16(outMix, 0) == unchecked((short)0xFFFE) ? Marshal.ReadInt32(outMix, 20) : 0;
                outFmt = Marshal.AllocCoTaskMem(40);
                Marshal.WriteInt16(outFmt, 0, unchecked((short)0xFFFE)); Marshal.WriteInt16(outFmt, 2, (short)och); Marshal.WriteInt32(outFmt, 4, rate);
                Marshal.WriteInt32(outFmt, 8, rate * och * 4); Marshal.WriteInt16(outFmt, 12, (short)(och * 4)); Marshal.WriteInt16(outFmt, 14, 32);
                Marshal.WriteInt16(outFmt, 16, 22); Marshal.WriteInt16(outFmt, 18, 32); Marshal.WriteInt32(outFmt, 20, mask);
                Marshal.Copy(FloatSub.ToByteArray(), 0, outFmt + 24, 16);
                hr = outC.Initialize(0, 0x80000000u | 0x08000000u, 300000, 0, outFmt, IntPtr.Zero);   // 30 ms buffer, rate conversion by Windows
                if (hr < 0) { Error = "Couldn't start the headset (0x" + hr.ToString("X8") + ")."; return; }
                uint outFrames; outC.GetBufferSize(out outFrames);
                long odef, omin; outC.GetDevicePeriod(out odef, out omin); RenderLatencyMs = odef / 10000.0;   // the engine's pass on the headset
                Guid ciid = typeof(IAudioCaptureClient).GUID, riid = typeof(IAudioRenderClient).GUID;
                capC.GetService(ref ciid, out o); var cap = (IAudioCaptureClient)o;
                outC.GetService(ref riid, out o); var ren = (IAudioRenderClient)o;

                int period = Math.Max(16, (int)(rate * def / 10000000));
                int cushion = period / 2;                                // half a period of slack so the headset never runs dry
                uint maxQueue = (uint)(period + cushion * 2);            // and never much more than that waiting (old audio is dropped)
                var mono = new float[rate];                              // 1 s of scratch, allocated once
                var raw = new byte[rate * block];
                var inter = new float[rate * och];
                int have = 0; long queueSum = 0, queueN = 0;

                IntPtr pb;
                outC.Start(); capC.Start();
                while (running)
                {
                    if (!ev.WaitOne(200)) continue;
                    uint packet; cap.GetNextPacketSize(out packet);
                    while (packet > 0 && running)
                    {
                        IntPtr data; uint frames, flags; ulong p1, p2;
                        if (cap.GetBuffer(out data, out frames, out flags, out p1, out p2) < 0) break;
                        int bytes = (int)frames * block;
                        if (have + frames > mono.Length) have = 0;   // can't happen with a 10 ms period; never grow the buffer here
                        if ((flags & 2) != 0 || bytes > raw.Length) { for (int f = 0; f < frames; f++) mono[have + f] = 0; }
                        else
                        {
                            Marshal.Copy(data, raw, 0, bytes);
                            for (int f = 0; f < frames; f++)
                            {
                                double sum = 0;
                                for (int c = 0; c < ch; c++)
                                {
                                    int off = f * block + c * (bits / 8);
                                    sum += isFloat ? BitConverter.ToSingle(raw, off)
                                         : bits == 16 ? BitConverter.ToInt16(raw, off) / 32768.0
                                         : bits == 32 ? BitConverter.ToInt32(raw, off) / 2147483648.0
                                         : (raw[off] | (raw[off + 1] << 8) | ((sbyte)raw[off + 2] << 16)) / 8388608.0;
                                }
                                mono[have + f] = (float)(sum / ch);
                            }
                        }
                        cap.ReleaseBuffer(frames);
                        have += (int)frames; Captured += frames;
                        cap.GetNextPacketSize(out packet);
                    }
                    if (have == 0) continue;
                    uint pad; outC.GetCurrentPadding(out pad);
                    if (pad == 0)
                    {
                        if (Rendered > 0) Underruns++;
                        if (ren.GetBuffer((uint)cushion, out pb) >= 0) { ren.ReleaseBuffer((uint)cushion, 2); pad = (uint)cushion; }   // (re)build the cushion with silence
                    }
                    queueSum += pad; queueN++; QueueMs = queueSum * 1000.0 / queueN / rate;
                    uint room = pad >= maxQueue ? 0 : maxQueue - pad; if (room > outFrames - pad) room = outFrames - pad;
                    int write = (int)Math.Min(room, (uint)have);
                    int skip = have - write;                                // drop the oldest so the delay can't build up
                    if (skip > 0) Dropped += skip;
                    if (write > 0 && ren.GetBuffer((uint)write, out pb) >= 0)
                    {
                        float g = Silent ? 0f : Gain;
                        if (g <= 0f) ren.ReleaseBuffer((uint)write, 2);   // silent
                        else
                        {
                            int k = 0;
                            for (int f = 0; f < write; f++)
                            {
                                float v = mono[skip + f] * g; if (v > 1f) v = 1f; else if (v < -1f) v = -1f;
                                for (int c = 0; c < och; c++) inter[k++] = v;
                            }
                            Marshal.Copy(inter, 0, pb, k);
                            ren.ReleaseBuffer((uint)write, 0);
                        }
                        Rendered += write;
                    }
                    have = 0;
                }
            }
            catch (Exception ex) { Error = "Sidetone stopped: " + ex.Message; }
            finally
            {
                try { if (capC != null) capC.Stop(); } catch { }
                try { if (outC != null) outC.Stop(); } catch { }
                if (capFmt != IntPtr.Zero) Marshal.FreeCoTaskMem(capFmt);
                if (outMix != IntPtr.Zero) Marshal.FreeCoTaskMem(outMix);
                if (outFmt != IntPtr.Zero) Marshal.FreeCoTaskMem(outFmt);
                if (mmcss != IntPtr.Zero) try { AvRevertMmThreadCharacteristics(mmcss); } catch { }
                if (capC != null) Marshal.ReleaseComObject(capC);
                if (outC != null) Marshal.ReleaseComObject(outC);
                ev.Dispose();
                running = false;
            }
        }
    }
}
