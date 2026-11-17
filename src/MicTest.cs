// Hallow Mixer - mic test: record a few seconds from a mic (exactly what apps get) and play it back.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace HallowMixer
{
    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioClient
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
    }
    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devPos, out ulong qpc);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }

    public static class MicTest
    {
        static readonly Guid FloatSub = new Guid("00000003-0000-0010-8000-00aa00389b71");

        // Records from the mic and returns a 16-bit WAV (mono) ready to play, plus the loudest level heard (0..1).
        public static byte[] Record(string micId, int seconds, out float peak)
        {
            peak = 0;
            var en = (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
            IMMDevice dev; if (en.GetDevice(micId, out dev) < 0) throw new Exception("Mic not found");
            Guid iid = typeof(IAudioClient).GUID; object o;
            if (dev.Activate(ref iid, 23, IntPtr.Zero, out o) < 0) throw new Exception("Couldn't open the mic");
            var client = (IAudioClient)o;
            IntPtr fmt; client.GetMixFormat(out fmt);
            try
            {
                int tag = Marshal.ReadInt16(fmt, 0), channels = Marshal.ReadInt16(fmt, 2), rate = Marshal.ReadInt32(fmt, 4);
                int block = Marshal.ReadInt16(fmt, 12), bits = Marshal.ReadInt16(fmt, 14);
                bool isFloat = tag == 3;
                if (tag == unchecked((short)0xFFFE)) { var sub = new byte[16]; Marshal.Copy(fmt + 24, sub, 0, 16); isFloat = new Guid(sub) == FloatSub; }
                int hr = client.Initialize(0, 0, 10000000, 0, fmt, IntPtr.Zero);   // shared mode, 1 s buffer
                if (hr < 0) throw new Exception("Couldn't start recording (0x" + hr.ToString("X8") + ")");
                Guid ciid = typeof(IAudioCaptureClient).GUID; object co; client.GetService(ref ciid, out co);
                var cap = (IAudioCaptureClient)co;
                var mono = new MemoryStream();
                client.Start();
                var end = DateTime.UtcNow.AddSeconds(seconds);
                var tmp = new byte[0];
                while (DateTime.UtcNow < end)
                {
                    Thread.Sleep(15);
                    uint packet; cap.GetNextPacketSize(out packet);
                    while (packet > 0)
                    {
                        IntPtr data; uint frames, flags; ulong p1, p2;
                        if (cap.GetBuffer(out data, out frames, out flags, out p1, out p2) < 0) break;
                        int bytes = (int)frames * block;
                        if (tmp.Length < bytes) tmp = new byte[bytes];
                        if ((flags & 2) != 0) Array.Clear(tmp, 0, bytes); else Marshal.Copy(data, tmp, 0, bytes);
                        cap.ReleaseBuffer(frames);
                        for (int f = 0; f < frames; f++)
                        {
                            double sum = 0;
                            for (int c = 0; c < channels; c++)
                            {
                                int off = f * block + c * (bits / 8);
                                double s = isFloat ? BitConverter.ToSingle(tmp, off)
                                         : bits == 16 ? BitConverter.ToInt16(tmp, off) / 32768.0
                                         : bits == 32 ? BitConverter.ToInt32(tmp, off) / 2147483648.0
                                         : (tmp[off] | (tmp[off + 1] << 8) | ((sbyte)tmp[off + 2] << 16)) / 8388608.0;   // 24-bit
                                sum += s;
                            }
                            double m = Math.Max(-1, Math.Min(1, sum / channels));
                            if (Math.Abs(m) > peak) peak = (float)Math.Abs(m);
                            short v = (short)(m * 32767);
                            mono.WriteByte((byte)(v & 0xFF)); mono.WriteByte((byte)((v >> 8) & 0xFF));
                        }
                        cap.GetNextPacketSize(out packet);
                    }
                }
                client.Stop();
                return Wav(mono.ToArray(), rate);
            }
            finally { Marshal.FreeCoTaskMem(fmt); }
        }

        static byte[] Wav(byte[] pcm, int rate)
        {
            var ms = new MemoryStream(); var w = new BinaryWriter(ms);
            w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' }); w.Write(36 + pcm.Length);
            w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' }); w.Write(pcm.Length); w.Write(pcm);
            return ms.ToArray();
        }
    }
}
