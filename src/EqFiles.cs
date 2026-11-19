// Hallow Mixer - v3.19 EQ presets you can share: your own saved presets, export / import, and reading text you paste in -
// AutoEQ's ParametricEQ.txt / GraphicEQ.txt or any Equalizer APO "Filter:" lines. Nothing is downloaded.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace HallowMixer
{
    [DataContract] public class EqUserPreset
    {
        [DataMember] public string HallowEq = "1";   // marks a Hallow Mixer EQ preset file
        [DataMember] public string Name;
        [DataMember] public bool Capture;             // a mic preset
        [DataMember] public bool Param;
        [DataMember] public int[] Gains = new int[10];
        [DataMember] public List<PBand> PBands;
        [DataMember] public int LowCut;
    }

    public static class EqFiles
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static double Cl(double v, double lo, double hi) { return double.IsNaN(v) || double.IsInfinity(v) ? lo : Math.Max(lo, Math.Min(hi, v)); }

        public static EqUserPreset From(EqState st, string name)
        {
            bool param = st.Param && st.PBands != null;
            return Clean(new EqUserPreset { Name = name, Capture = st.Capture, Param = param, Gains = (int[])(st.Gains ?? new int[10]).Clone(),
                PBands = param ? PEq.Copy(st.PBands) : null, LowCut = st.Capture ? st.LowCut : 0 });
        }
        // Puts a preset on a device (its other settings - headphone space, mic chain - stay).
        public static void ApplyTo(EqUserPreset p, EqState st)
        {
            st.Param = p.Param && p.PBands != null && p.PBands.Count > 0;
            if (st.Param) st.PBands = PEq.Copy(p.PBands); else st.Gains = (int[])p.Gains.Clone();
            if (st.Capture && p.Capture) st.LowCut = p.LowCut;
            st.Preset = p.Name; st.On = true;
        }

        // Everything inside the ranges the EQ window allows (a file may come from anyone).
        public static EqUserPreset Clean(EqUserPreset p)
        {
            if (p == null) return null;
            var g = new int[10]; if (p.Gains != null) for (int i = 0; i < 10 && i < p.Gains.Length; i++) g[i] = (int)Cl(p.Gains[i], -12, 12);
            var name = string.IsNullOrWhiteSpace(p.Name) ? "My EQ" : p.Name.Trim(); if (name.Length > 40) name = name.Substring(0, 40);
            var r = new EqUserPreset { Name = name, Capture = p.Capture, Gains = g, LowCut = p.Capture && Eq.LowCuts.Contains(p.LowCut) ? p.LowCut : 0 };
            if (p.PBands != null)
                r.PBands = p.PBands.Where(b => b != null).Take(10).Select(b => new PBand {
                    Type = PEq.Types.Contains(b.Type) ? b.Type : "PK", Freq = Cl(b.Freq, 20, 20000), Gain = Cl(b.Gain, -15, 15), Q = Cl(b.Q, 0.1, 10), On = b.On }).ToList();
            r.Param = p.Param && r.PBands != null && r.PBands.Count > 0;
            return r;
        }

        public static string ToJson(EqUserPreset p)
        {
            using (var m = new MemoryStream()) { new DataContractJsonSerializer(typeof(EqUserPreset)).WriteObject(m, Clean(p)); return Encoding.UTF8.GetString(m.ToArray()); }
        }
        // Equalizer APO text (also works in Peace and other Equalizer APO setups).
        public static string ToApoText(EqUserPreset p)
        {
            p = Clean(p);
            var sb = new StringBuilder();
            sb.AppendLine("# " + p.Name + " - exported from Hallow Mixer");
            double max = p.Param ? Math.Ceiling(PEq.MaxBoost(p.PBands) * 10) / 10 : Math.Max(0, p.Gains.Max());
            sb.AppendLine("Preamp: " + (-max).ToString("0.#", Inv) + " dB");
            if (p.Param) foreach (var b in p.PBands.Where(PEq.Active)) sb.AppendLine(PEq.Line(b));
            else sb.AppendLine("GraphicEQ: " + string.Join("; ", Eq.Bands.Select((f, i) => f.ToString(Inv) + " " + p.Gains[i].ToString(Inv))));
            if (p.LowCut > 0) sb.AppendLine("Filter: ON HP Fc " + p.LowCut.ToString(Inv) + " Hz");
            return sb.ToString();
        }

        static readonly Regex FilterRx = new Regex(@"^\s*Filter\s*\d*\s*:\s*(ON|OFF)\s+([A-Z]+)(?:\s+\d+\s*dB)?\s+Fc\s+([0-9.]+)\s*Hz(?:\s+Gain\s+(-?[0-9.]+)\s*dB)?(?:\s+Q\s+([0-9.]+))?", RegexOptions.IgnoreCase);
        static readonly Regex GraphicRx = new Regex(@"^\s*GraphicEQ\s*:\s*(.+)$", RegexOptions.IgnoreCase);

        // Reads a Hallow preset file, AutoEQ ParametricEQ.txt / GraphicEQ.txt or Equalizer APO filter lines.
        // Returns null (and why) if nothing usable is in it. note says what was skipped.
        public static EqUserPreset Parse(string text, string name, out string note)
        {
            note = null;
            if (string.IsNullOrWhiteSpace(text)) { note = "It's empty."; return null; }
            if (text.Length > 512 * 1024) { note = "That's far too long for an EQ preset."; return null; }
            var t = text.Trim();
            if (t.StartsWith("{"))
            {
                try
                {
                    EqUserPreset p;
                    using (var m = new MemoryStream(Encoding.UTF8.GetBytes(t))) p = (EqUserPreset)new DataContractJsonSerializer(typeof(EqUserPreset)).ReadObject(m);
                    if (p == null || p.HallowEq == null) { note = "That isn't a Hallow Mixer EQ preset."; return null; }
                    if (string.IsNullOrWhiteSpace(p.Name)) p.Name = name;
                    return Clean(p);
                }
                catch { note = "That isn't a Hallow Mixer EQ preset."; return null; }
            }
            var bands = new List<PBand>(); int skipped = 0, off = 0; int[] graphic = null;
            foreach (var raw in t.Split('\n'))
            {
                var line = raw.Trim(); if (line.Length == 0 || line.StartsWith("#")) continue;
                var m = FilterRx.Match(line);
                if (m.Success)
                {
                    if (m.Groups[1].Value.ToUpperInvariant() == "OFF") { off++; continue; }
                    string type; double q = 0;
                    switch (m.Groups[2].Value.ToUpperInvariant())
                    {
                        case "PK": case "PEQ": case "MODAL": type = "PK"; q = 1; break;
                        case "LS": case "LSC": case "LSQ": type = "LS"; q = 0.71; break;
                        case "HS": case "HSC": case "HSQ": type = "HS"; q = 0.71; break;
                        case "HP": case "HPQ": type = "HP"; q = 0.71; break;
                        case "LP": case "LPQ": type = "LP"; q = 0.71; break;
                        default: skipped++; continue;   // notch, band pass, all pass...
                    }
                    double f, g = 0, qq;
                    if (!double.TryParse(m.Groups[3].Value, NumberStyles.Float, Inv, out f)) { skipped++; continue; }
                    if (m.Groups[4].Success) double.TryParse(m.Groups[4].Value, NumberStyles.Float, Inv, out g);
                    if (m.Groups[5].Success && double.TryParse(m.Groups[5].Value, NumberStyles.Float, Inv, out qq)) q = qq;
                    bands.Add(new PBand { Type = type, Freq = f, Gain = g, Q = q, On = true });
                    continue;
                }
                var gm = GraphicRx.Match(line);
                if (gm.Success) graphic = Graphic(gm.Groups[1].Value);
            }
            if (bands.Count > 0)
            {
                if (bands.Count > 10) { note = "Only the first 10 of " + bands.Count + " filters were kept."; bands = bands.Take(10).ToList(); }
                else if (skipped > 0) note = skipped + " filter(s) of a kind Hallow Mixer doesn't have were skipped.";
                return Clean(new EqUserPreset { Name = name, Param = true, PBands = bands });
            }
            if (graphic != null) return Clean(new EqUserPreset { Name = name, Param = false, Gains = graphic });
            note = off > 0 ? "All its filters are switched off." : "No EQ found in it - paste Equalizer APO / AutoEQ text (Filter: or GraphicEQ: lines).";
            return null;
        }

        // A GraphicEQ curve ("20 -1.2; 21 -1.3; ...") read at our 10 bands (log-frequency interpolation), rounded to whole dB.
        static int[] Graphic(string pairs)
        {
            var pts = new List<Tuple<double, double>>();
            foreach (var p in pairs.Split(';'))
            {
                var a = p.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries); double f, g;
                if (a.Length >= 2 && double.TryParse(a[0], NumberStyles.Float, Inv, out f) && double.TryParse(a[1], NumberStyles.Float, Inv, out g) && f > 0) pts.Add(Tuple.Create(Math.Log(f), g));
            }
            if (pts.Count == 0) return null;
            pts = pts.OrderBy(x => x.Item1).ToList();
            var res = new int[10];
            for (int i = 0; i < 10; i++)
            {
                double x = Math.Log(Eq.Bands[i]), y;
                if (x <= pts[0].Item1) y = pts[0].Item2;
                else if (x >= pts[pts.Count - 1].Item1) y = pts[pts.Count - 1].Item2;
                else { int k = 1; while (pts[k].Item1 < x) k++; var a = pts[k - 1]; var b = pts[k]; y = b.Item1 == a.Item1 ? b.Item2 : a.Item2 + (b.Item2 - a.Item2) * (x - a.Item1) / (b.Item1 - a.Item1); }
                res[i] = (int)Math.Round(Cl(y, -12, 12));
            }
            return res;
        }
    }
}
