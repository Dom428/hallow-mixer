// Hallow Mixer - auto mic level: you talk for 5 seconds, it sets the mic so your voice peaks around -7 dB
// (loud and clear, never clipping). Works in dB on the mic's own range, so it's right on any mic.
using System;
using System.Collections.Generic;
using System.Linq;

namespace HallowMixer
{
    public class MicSuggestion { public float NewDb; public string Note; public bool Changed; }

    public static class MicLevel
    {
        public const double Target = 0.45;   // speech peaks at about -7 dBFS

        // The level your voice really peaks at: the loud end of your speech, ignoring silence and the odd click.
        // wav = 16-bit mono WAV from MicTest.Record. Returns 0 when it heard no talking.
        public static double SpeechPeak(byte[] wav)
        {
            if (wav == null || wav.Length < 46) return 0;
            int rate = BitConverter.ToInt32(wav, 24); int block = Math.Max(1, rate / 20);   // 50 ms blocks
            var peaks = new List<double>();
            for (int start = 44; start + block * 2 <= wav.Length; start += block * 2)
            {
                double p = 0;
                for (int i = start; i < start + block * 2; i += 2) p = Math.Max(p, Math.Abs(BitConverter.ToInt16(wav, i) / 32768.0));
                peaks.Add(p);
            }
            var voiced = peaks.Where(p => p > 0.02).OrderBy(p => p).ToList();
            if (voiced.Count < 6) return 0;                           // under 0.3 s of sound: you didn't talk
            return voiced[(int)Math.Floor((voiced.Count - 1) * 0.9)];   // 90th percentile - a single click can't fool it
        }

        public static MicSuggestion Suggest(float curDb, float minDb, float maxDb, double speechPeak)
        {
            if (speechPeak <= 0) return new MicSuggestion { NewDb = curDb, Note = "Didn't hear you talking - check the mic isn't muted on the mic itself, then try again." };
            double gain = 20 * Math.Log10(Target / Math.Min(speechPeak, 0.999));
            if (speechPeak >= 0.98) gain = Math.Min(gain, -6);           // clipping: the real level was even higher
            var nd = (float)Math.Max(minDb, Math.Min(maxDb, curDb + gain));
            string note;
            if (Math.Abs(nd - curDb) < 0.75) note = "Your mic level is already right.";
            else if (nd >= maxDb - 0.01 && curDb + gain > maxDb + 1) note = "Turned all the way up and you're still a bit quiet - use Mic Boost (Hallow Toolkit > Mic) or move closer to the mic.";
            else if (speechPeak >= 0.98) note = "You were clipping (too loud) - turned down. Try once more to fine-tune.";
            else note = gain > 0 ? "Turned up so you're clearer." : "Turned down so you don't distort.";
            return new MicSuggestion { NewDb = nd, Note = note, Changed = Math.Abs(nd - curDb) >= 0.75 };
        }
    }
}
