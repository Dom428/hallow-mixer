// Hallow Mixer - v3.21 loudness meter: LUFS (ITU-R BS.1770 / EBU R128 momentary 400 ms and short-term 3 s) of what you hear,
// read from Windows' loopback of the default output (read only - nothing is played or changed).
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace HallowMixer
{
    // K-weighting + mean square in 100 ms blocks. Any sample rate, any number of channels (all weighted 1.0).
    public class LufsMeter
    {
        readonly int rate, ch, blockLen;
        readonly double[] pb = new double[3], pa = new double[3], rb = { 1, -2, 1 }, ra = new double[3];
        readonly double[,] s1, s2;                 // filter state per channel: [ch, 0..3] = x1,x2,y1,y2
        readonly double[] blocks = new double[30]; int nBlocks, head;
        double acc; int accN;

        public LufsMeter(int sampleRate, int channels)
        {
            rate = sampleRate; ch = Math.Max(1, channels); blockLen = Math.Max(1, rate / 10);
            s1 = new double[ch, 4]; s2 = new double[ch, 4];
            // pre-filter (high shelf) and RLB high-pass, for this sample rate (the standard's analog prototypes, bilinear)
            double f0 = 1681.974450955533, G = 3.999843853973347, Q = 0.7071752369554196;
            double K = Math.Tan(Math.PI * f0 / rate), Vh = Math.Pow(10, G / 20), Vb = Math.Pow(Vh, 0.4996667741545416), a0 = 1 + K / Q + K * K;
            pb[0] = (Vh + Vb * K / Q + K * K) / a0; pb[1] = 2 * (K * K - Vh) / a0; pb[2] = (Vh - Vb * K / Q + K * K) / a0;
            pa[0] = 1; pa[1] = 2 * (K * K - 1) / a0; pa[2] = (1 - K / Q + K * K) / a0;
            f0 = 38.13547087602444; Q = 0.5003270373238773; K = Math.Tan(Math.PI * f0 / rate); a0 = 1 + K / Q + K * K;
            ra[0] = 1; ra[1] = 2 * (K * K - 1) / a0; ra[2] = (1 - K / Q + K * K) / a0;
        }
        static double Biquad(double x, double[] b, double[] a, double[,] s, int c)
        {
            double y = b[0] * x + b[1] * s[c, 0] + b[2] * s[c, 1] - a[1] * s[c, 2] - a[2] * s[c, 3];
            s[c, 1] = s[c, 0]; s[c, 0] = x; s[c, 3] = s[c, 2]; s[c, 2] = Math.Abs(y) < 1e-30 ? 0 : y;
            return y;
        }
        // interleaved float samples
        public void Add(float[] x, int frames)
        {
            for (int f = 0; f < frames; f++)
            {
                double sum = 0;
                for (int c = 0; c < ch; c++)
                {
                    double v = x[f * ch + c]; if (double.IsNaN(v) || double.IsInfinity(v)) v = 0;
                    v = Biquad(Biquad(v, pb, pa, s1, c), rb, ra, s2, c);
                    sum += v * v;
                }
                acc += sum; accN++;
                if (accN >= blockLen) { blocks[head] = acc / accN; head = (head + 1) % blocks.Length; if (nBlocks < blocks.Length) nBlocks++; acc = 0; accN = 0; }
            }
        }
        double Over(int n)
        {
            if (nBlocks < n) n = nBlocks; if (n == 0) return double.NegativeInfinity;
            double s = 0; for (int i = 1; i <= n; i++) s += blocks[(head - i + blocks.Length) % blocks.Length];
            s /= n; return s <= 1e-12 ? double.NegativeInfinity : -0.691 + 10 * Math.Log10(s);
        }
        public double Momentary { get { return Over(4); } }     // 400 ms
        public double ShortTerm { get { return Over(30); } }    // 3 s
    }

    // Reads the default output through Windows' loopback while the mixer window is open.
    public class LoudnessReader
    {
        public volatile float Momentary = float.NegativeInfinity, ShortTerm = float.NegativeInfinity;
        Thread thread; volatile bool running;
        static readonly Guid FloatSub = new Guid("00000003-0000-0010-8000-00aa00389b71");

        public void Start()
        {
            if (running) return;
            running = true;
            thread = new Thread(Run) { IsBackground = true, Name = "HallowMixer loudness" };
            thread.SetApartmentState(ApartmentState.MTA); thread.Start();
        }
        public void Stop() { running = false; Momentary = ShortTerm = float.NegativeInfinity; }

        void Run()
        {
            while (running)
            {
                try { Read(); } catch { }
                for (int i = 0; i < 20 && running; i++) Thread.Sleep(100);   // device gone or changed: try again in 2 s
            }
        }
        void Read()
        {
            var en = (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
            var id = CoreAudio.DefaultId(0); if (id == null) return;
            IMMDevice dev; if (en.GetDevice(id, out dev) < 0) return;
            Guid iid = typeof(IAudioClient).GUID; object o;
            if (dev.Activate(ref iid, 23, IntPtr.Zero, out o) < 0) return;
            var client = (IAudioClient)o; IntPtr fmt; client.GetMixFormat(out fmt);
            try
            {
                int tag = Marshal.ReadInt16(fmt, 0), ch = Marshal.ReadInt16(fmt, 2), rate = Marshal.ReadInt32(fmt, 4), block = Marshal.ReadInt16(fmt, 12), bits = Marshal.ReadInt16(fmt, 14);
                bool isFloat = tag == 3; if (tag == unchecked((short)0xFFFE)) { var sub = new byte[16]; Marshal.Copy(fmt + 24, sub, 0, 16); isFloat = new Guid(sub) == FloatSub; }
                if (client.Initialize(0, 0x20000, 2000000, 0, fmt, IntPtr.Zero) < 0) return;   // shared loopback, 200 ms buffer
                Guid ciid = typeof(IAudioCaptureClient).GUID; object co; client.GetService(ref ciid, out co);
                var cap = (IAudioCaptureClient)co;
                var meter = new LufsMeter(rate, ch);
                var raw = new byte[rate * block]; var buf = new float[rate * ch];
                client.Start();
                try
                {
                    var started = DateTime.UtcNow;
                    while (running && CoreAudio.DefaultId(0) == id)
                    {
                        Thread.Sleep(50);
                        uint packet; cap.GetNextPacketSize(out packet); bool any = false;
                        while (packet > 0)
                        {
                            IntPtr data; uint frames, flags; ulong p1, p2;
                            if (cap.GetBuffer(out data, out frames, out flags, out p1, out p2) < 0) break;
                            int n = (int)Math.Min(frames, (uint)rate), bytes = n * block;
                            if ((flags & 2) != 0) Array.Clear(buf, 0, n * ch);
                            else
                            {
                                Marshal.Copy(data, raw, 0, bytes);
                                for (int i = 0; i < n * ch; i++)
                                {
                                    int off = i * (bits / 8);
                                    buf[i] = isFloat ? BitConverter.ToSingle(raw, off) : bits == 16 ? BitConverter.ToInt16(raw, off) / 32768f
                                           : bits == 32 ? BitConverter.ToInt32(raw, off) / 2147483648f : (raw[off] | (raw[off + 1] << 8) | ((sbyte)raw[off + 2] << 16)) / 8388608f;
                                }
                            }
                            cap.ReleaseBuffer(frames);
                            meter.Add(buf, n); any = true;
                            cap.GetNextPacketSize(out packet);
                        }
                        // loopback sends nothing while nothing plays: count that as silence
                        if (!any) { Array.Clear(buf, 0, rate / 20 * ch); meter.Add(buf, rate / 20); }
                        Momentary = (float)meter.Momentary; ShortTerm = (float)meter.ShortTerm;
                        if ((DateTime.UtcNow - started).TotalMinutes > 30) break;   // re-open now and then (device format changes)
                    }
                }
                finally { client.Stop(); }
            }
            finally { Marshal.FreeCoTaskMem(fmt); Marshal.ReleaseComObject(client); }
        }
    }
}
