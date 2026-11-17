// Hallow Mixer - is someone really talking? Used by auto-ducking so short sounds (Discord join/leave, notification
// pings, a key click on your mic) and steady background noise don't turn your game down.
using System;
using System.Collections.Generic;
using System.Linq;

namespace HallowMixer
{
    public class VoiceDetector
    {
        public bool Smart = true;          // false = the old way: any sound over the threshold ducks
        public float Threshold;            // minimum level that counts as sound
        float floor;                       // background level (follows quiet moments fast, steady sound slowly)
        bool above;                        // inside a burst of sound (for counting syllable starts)
        DateTime holdUntil;
        readonly Queue<Tuple<DateTime, bool, bool>> hist = new Queue<Tuple<DateTime, bool, bool>>();   // time, sound, syllable start

        static readonly TimeSpan Window = TimeSpan.FromMilliseconds(900);
        static readonly TimeSpan Hold = TimeSpan.FromMilliseconds(900);       // keeps ducking between words
        static readonly TimeSpan SimpleHold = TimeSpan.FromMilliseconds(700);

        public VoiceDetector(float threshold) { Threshold = threshold; floor = threshold / 3; }

        public bool Talking { get; private set; }

        // Call ~20 times a second with the channel's peak level (0..1).
        public bool Update(float level, DateTime now)
        {
            if (level < floor) floor += (level - floor) * 0.25f; else floor += (level - floor) * 0.012f;
            float th = Math.Max(Threshold, floor * 2.2f);
            bool sound = level > th;
            if (!Smart)
            {
                if (sound) holdUntil = now + SimpleHold;
                return Talking = now < holdUntil;
            }
            bool start = sound && !above;
            if (sound) above = true; else if (level < th * 0.75f) above = false;
            hist.Enqueue(Tuple.Create(now, sound, start));
            while (hist.Count > 0 && now - hist.Peek().Item1 > Window) hist.Dequeue();

            if (now < holdUntil)
            {
                if (sound) holdUntil = now + Hold;                 // still talking
            }
            else if (sound && hist.Count >= 6)
            {
                // speech: sound for at least half a second, mostly on, with three or more separate syllable starts
                var recent = hist.SkipWhile(h => !h.Item2).ToList();   // from the first sound in the window
                double span = (now - recent[0].Item1).TotalMilliseconds;
                int on = recent.Count(h => h.Item2), starts = recent.Count(h => h.Item3);
                if (span >= 500 && on >= recent.Count * 0.5 && starts >= 3) holdUntil = now + Hold;
            }
            return Talking = now < holdUntil;
        }
    }
}
