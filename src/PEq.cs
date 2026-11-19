// Hallow Mixer - parametric EQ: bands you place anywhere (peak, shelves, low/high cut), drawn as a curve you drag.
// Equalizer APO runs the filters; the curve here uses the same standard (RBJ) filter maths so what you see is what you hear.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HallowMixer
{
    [DataContract] public class PBand
    {
        [DataMember] public string Type = "PK";   // PK peak, LS low shelf, HS high shelf, HP low cut, LP high cut
        [DataMember] public double Freq = 1000;
        [DataMember] public double Gain;
        [DataMember] public double Q = 1;
        [DataMember] public bool On = true;
        public PBand Clone() { return (PBand)MemberwiseClone(); }
        public static PBand P(string t, double f, double g, double q) { return new PBand { Type = t, Freq = f, Gain = g, Q = q, On = true }; }
    }

    public static class PEq
    {
        const double Fs = 48000;
        public static readonly string[] Types = { "PK", "LS", "HS", "HP", "LP" };
        public static readonly string[] TypeNames = { "Peak", "Low shelf", "High shelf", "Low cut", "High cut" };
        public static bool HasGain(PBand b) { return b.Type != "HP" && b.Type != "LP"; }
        public static bool Active(PBand b) { return b.On && (!HasGain(b) || Math.Abs(b.Gain) >= 0.05); }

        // Standard biquad coefficients (Robert Bristow-Johnson's Audio EQ Cookbook) - the same filters Equalizer APO uses.
        static double[] Coeffs(PBand b)
        {
            double A = Math.Pow(10, b.Gain / 40), w0 = 2 * Math.PI * Math.Min(b.Freq, Fs / 2.1) / Fs, cs = Math.Cos(w0), sn = Math.Sin(w0);
            double alpha = sn / (2 * Math.Max(0.1, b.Q)), sq = 2 * Math.Sqrt(A) * alpha;
            switch (b.Type)
            {
                case "LS": return new[] { A * ((A + 1) - (A - 1) * cs + sq), 2 * A * ((A - 1) - (A + 1) * cs), A * ((A + 1) - (A - 1) * cs - sq),
                                          (A + 1) + (A - 1) * cs + sq, -2 * ((A - 1) + (A + 1) * cs), (A + 1) + (A - 1) * cs - sq };
                case "HS": return new[] { A * ((A + 1) + (A - 1) * cs + sq), -2 * A * ((A - 1) + (A + 1) * cs), A * ((A + 1) + (A - 1) * cs - sq),
                                          (A + 1) - (A - 1) * cs + sq, 2 * ((A - 1) - (A + 1) * cs), (A + 1) - (A - 1) * cs - sq };
                case "HP": return new[] { (1 + cs) / 2, -(1 + cs), (1 + cs) / 2, 1 + alpha, -2 * cs, 1 - alpha };
                case "LP": return new[] { (1 - cs) / 2, 1 - cs, (1 - cs) / 2, 1 + alpha, -2 * cs, 1 - alpha };
                default: return new[] { 1 + alpha * A, -2 * cs, 1 - alpha * A, 1 + alpha / A, -2 * cs, 1 - alpha / A };
            }
        }
        // Gain of one band at frequency f, in dB.
        public static double Db(PBand b, double f)
        {
            var c = Coeffs(b); double w = 2 * Math.PI * f / Fs;
            double c1 = Math.Cos(w), s1 = Math.Sin(w), c2 = Math.Cos(2 * w), s2 = Math.Sin(2 * w);
            double nr = c[0] + c[1] * c1 + c[2] * c2, ni = -(c[1] * s1 + c[2] * s2);
            double dr = c[3] + c[4] * c1 + c[5] * c2, di = -(c[4] * s1 + c[5] * s2);
            double mag = Math.Sqrt((nr * nr + ni * ni) / Math.Max(1e-30, dr * dr + di * di));
            return 20 * Math.Log10(Math.Max(1e-10, mag));
        }
        public static double Response(IEnumerable<PBand> bands, double f) { double s = 0; foreach (var b in bands) if (Active(b)) s += Db(b, f); return s; }
        // The highest boost anywhere on the curve (the preamp turns everything down by this, so nothing distorts).
        public static double MaxBoost(IEnumerable<PBand> bands)
        {
            var list = bands.ToList(); double m = 0;
            for (int i = 0; i <= 160; i++) m = Math.Max(m, Response(list, 20 * Math.Pow(1000, i / 160.0)));
            return m;
        }
        // One line of Equalizer APO config.
        public static string Line(PBand b)
        {
            var c = CultureInfo.InvariantCulture;
            string fc = " Fc " + b.Freq.ToString("0.#", c) + " Hz", g = " Gain " + b.Gain.ToString("0.#", c) + " dB", q = " Q " + b.Q.ToString("0.##", c);
            switch (b.Type)
            {
                case "LS": return "Filter: ON LSC" + fc + g + q;
                case "HS": return "Filter: ON HSC" + fc + g + q;
                case "HP": return "Filter: ON HPQ" + fc + q;
                case "LP": return "Filter: ON LPQ" + fc + q;
                default: return "Filter: ON PK" + fc + g + q;
            }
        }
        // Switching from graphic to parametric keeps your sound: every graphic band that isn't 0 becomes a peak band.
        public static List<PBand> FromGraphic(int[] gains)
        {
            var res = new List<PBand>();
            if (gains != null) for (int i = 0; i < Math.Min(10, gains.Length); i++) if (gains[i] != 0) res.Add(PBand.P("PK", Eq.Bands[i], gains[i], 1.41));
            if (res.Count > 0) return res;
            return new List<PBand> { PBand.P("LS", 100, 0, 0.71), PBand.P("PK", 400, 0, 1), PBand.P("PK", 1500, 0, 1), PBand.P("PK", 4000, 0, 1), PBand.P("HS", 10000, 0, 0.71) };
        }
        public static List<PBand> Copy(IEnumerable<PBand> src) { return src.Select(b => b.Clone()).ToList(); }

        static PBand pk(double f, double g, double q) { return PBand.P("PK", f, g, q); }
        static PBand ls(double f, double g) { return PBand.P("LS", f, g, 0.71); }
        static PBand hs(double f, double g) { return PBand.P("HS", f, g, 0.71); }
        static PBand hp(double f) { return PBand.P("HP", f, 0, 0.71); }

        // Starting points, not magic: game presets lift footsteps/reloads (2-5 kHz) and trim boomy lows; drag the dots to taste.
        public static readonly Dictionary<string, PBand[]> OutPresets = new Dictionary<string, PBand[]> {
            { "Valorant", new[] { ls(80, -3), pk(250, -2, 1), pk(2500, 3, 1.2), pk(4500, 4, 1.5), hs(10000, -1) } },
            { "CS2", new[] { ls(100, -4), pk(400, -2, 1), pk(3000, 4, 1.4), pk(6000, 2, 2), hs(12000, -2) } },
            { "Apex Legends", new[] { ls(90, -2), pk(300, -2, 1), pk(2000, 3, 1), pk(4000, 4, 1.5), hs(11000, 1) } },
            { "Fortnite", new[] { ls(80, -3), pk(1500, 2, 1), pk(3500, 4, 1.3), hs(9000, 1) } },
            { "Call of Duty", new[] { ls(70, -4), pk(250, -3, 0.9), pk(2500, 3, 1), pk(5000, 3, 1.6) } },
            { "Rainbow Six Siege", new[] { ls(120, -4), pk(500, -2, 1), pk(3000, 5, 1.2), pk(7000, 2, 2) } },
            { "PUBG", new[] { ls(90, -3), pk(2000, 3, 1), pk(4000, 4, 1.4) } },
            { "Tarkov", new[] { ls(100, -5), pk(300, -2, 1), pk(2500, 4, 1), pk(5500, 3, 1.8) } },
            { "Overwatch 2", new[] { ls(80, -2), pk(1000, 1, 1), pk(3000, 3, 1.2), hs(10000, 1) } },
            { "MOBA", new[] { pk(2000, 2, 0.9), hs(8000, 2) } },
            { "Story games", new[] { ls(60, 4), pk(3000, 1, 1), hs(10000, 2) } },
            { "Music V-shape", new[] { ls(90, 5), pk(1000, -2, 0.8), hs(9000, 4) } },
            { "Movie night", new[] { ls(70, 3), pk(2500, 2, 1), hs(12000, 1) } },
            { "Voices & podcasts", new[] { hp(100), pk(300, -2, 1), pk(3000, 3, 1) } } };
        public static readonly Dictionary<string, PBand[]> MicPresets = new Dictionary<string, PBand[]> {
            { "QuadCast 2S", new[] { hp(80), pk(250, -3, 1.2), pk(4500, 3, 1.2), pk(7000, -2, 3), hs(12000, 1) } },
            { "Broadcast warm", new[] { hp(70), ls(150, 2), pk(400, -2, 1), pk(3500, 2, 1) } },
            { "Bright & clear", new[] { hp(90), pk(300, -3, 1), hs(6000, 4) } },
            { "Less hiss (de-ess)", new[] { hp(80), pk(6500, -5, 3) } },
            { "Cut rumble & hum", new[] { hp(100), pk(60, -10, 8), pk(120, -6, 8) } } };
    }

    // The curve you drag: dots are bands (left/right = pitch, up/down = boost/cut, scroll = width). Double-click empty space adds a band.
    public class PEqEditor : StackPanel
    {
        public Action Changed;
        List<PBand> bands = new List<PBand>();
        int sel = -1; bool dragging, loading;
        const double W = 576, H = 210, MinF = 20, MaxF = 20000, Range = 15;
        static readonly string[] Colors = { "#F07820", "#A88BEB", "#6CCB7A", "#5AB4F0", "#E8C547", "#F06292", "#4DD0C8", "#FF8A65", "#B0BEC5", "#CE93D8" };
        readonly Canvas plot, dyn;
        readonly TextBlock title, fT, gT, qT, hint;
        readonly ComboBox type; readonly CheckBox on; readonly Slider freq, gain, q; readonly Button remove;

        static SolidColorBrush B(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        static double X(double f) { return Math.Log(f / MinF) / Math.Log(MaxF / MinF) * W; }
        static double F(double x) { return MinF * Math.Pow(MaxF / MinF, Math.Max(0, Math.Min(W, x)) / W); }
        static double Y(double db) { return H / 2 - Math.Max(-Range, Math.Min(Range, db)) / Range * (H / 2); }
        static double G(double y) { return Math.Max(-12, Math.Min(12, (H / 2 - y) / (H / 2) * Range)); }
        static double RoundF(double f) { return f < 100 ? Math.Round(f) : f < 1000 ? Math.Round(f / 5) * 5 : Math.Round(f / 50) * 50; }
        static string FText(double f) { return f >= 1000 ? (f / 1000).ToString(f >= 10000 ? "0.#" : "0.##", CultureInfo.InvariantCulture) + " kHz" : f.ToString("0", CultureInfo.InvariantCulture) + " Hz"; }

        public PEqEditor()
        {
            plot = new Canvas { Width = W, Height = H, ClipToBounds = true, Background = Brushes.Transparent, Cursor = Cursors.Cross };
            var gridBrush = B("#2E2838"); var lab = B("#6E6680");
            foreach (var f in new double[] { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 })
            {
                plot.Children.Add(new Line { X1 = X(f), X2 = X(f), Y1 = 0, Y2 = H, Stroke = gridBrush, StrokeThickness = 1 });
                var t = new TextBlock { Text = f >= 1000 ? (f / 1000) + "k" : f.ToString(CultureInfo.InvariantCulture), Foreground = lab, FontSize = 10 };
                Canvas.SetLeft(t, Math.Min(W - 22, X(f) + 3)); Canvas.SetTop(t, H - 15); plot.Children.Add(t);
            }
            foreach (var db in new double[] { 12, 6, 0, -6, -12 })
            {
                plot.Children.Add(new Line { X1 = 0, X2 = W, Y1 = Y(db), Y2 = Y(db), Stroke = db == 0 ? B("#4A4258") : gridBrush, StrokeThickness = 1 });
                var t = new TextBlock { Text = (db > 0 ? "+" : "") + db, Foreground = lab, FontSize = 10 };
                Canvas.SetLeft(t, 3); Canvas.SetTop(t, Y(db) - 14); plot.Children.Add(t);
            }
            dyn = new Canvas { Width = W, Height = H };
            plot.Children.Add(dyn);
            plot.MouseMove += OnMove;
            plot.MouseLeftButtonUp += (o, e) => { if (dragging) { dragging = false; plot.ReleaseMouseCapture(); } };
            plot.MouseLeftButtonDown += (o, e) =>
            {
                if (e.ClickCount == 2 && e.OriginalSource == plot && bands.Count < 10)
                {
                    var p = e.GetPosition(plot);
                    bands.Add(PBand.P("PK", RoundF(F(p.X)), Math.Round(G(p.Y) * 2) / 2, 1.41)); sel = bands.Count - 1;
                    Redraw(); Sync(); Fire();
                }
            };
            plot.MouseWheel += (o, e) =>
            {
                if (sel < 0 || sel >= bands.Count) return;
                var b = bands[sel]; b.Q = Math.Round(Math.Max(0.3, Math.Min(10, b.Q * (e.Delta > 0 ? 1.15 : 1 / 1.15))) * 100) / 100;
                e.Handled = true; Redraw(); Sync(); Fire();
            };
            Children.Add(new Border { CornerRadius = new CornerRadius(8), Background = B("#1E1926"), Padding = new Thickness(6), Child = plot });

            hint = new TextBlock { Text = "Drag a dot: left/right = pitch, up/down = boost or cut. Scroll = narrow/wide. Double-click empty space = new band.",
                Foreground = B("#8C84A0"), FontSize = 11, Margin = new Thickness(0, 6, 0, 8), TextWrapping = TextWrapping.Wrap };
            Children.Add(hint);

            // the selected band's controls
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            title = new TextBlock { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Width = 70 };
            on = new CheckBox { Content = "On", Style = (Style)FindResource("Switch"), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            on.Click += (o, e) => { if (Cur() == null) return; Cur().On = on.IsChecked == true; Redraw(); Fire(); };
            type = new ComboBox { Style = (Style)FindResource("Combo"), Width = 120, ItemsSource = PEq.TypeNames };
            type.SelectionChanged += (o, e) => { if (loading || Cur() == null || type.SelectedIndex < 0) return; Cur().Type = PEq.Types[type.SelectedIndex]; if (!PEq.HasGain(Cur())) Cur().Q = 0.71; Redraw(); Sync(); Fire(); };
            remove = new Button { Content = "Remove band", Style = (Style)FindResource("Btn") };
            remove.Click += (o, e) => { if (Cur() == null) return; bands.RemoveAt(sel); sel = Math.Min(sel, bands.Count - 1); Redraw(); Sync(); Fire(); };
            var add = new Button { Content = "Add band", Style = (Style)FindResource("Btn"), Margin = new Thickness(0, 0, 8, 0) };
            add.Click += (o, e) => { if (bands.Count >= 10) return; bands.Add(PBand.P("PK", 1000, 0, 1.41)); sel = bands.Count - 1; Redraw(); Sync(); Fire(); };
            DockPanel.SetDock(title, Dock.Left); DockPanel.SetDock(on, Dock.Left); DockPanel.SetDock(type, Dock.Left); DockPanel.SetDock(remove, Dock.Right); DockPanel.SetDock(add, Dock.Right);
            head.Children.Add(title); head.Children.Add(on); head.Children.Add(type); head.Children.Add(remove); head.Children.Add(add); head.Children.Add(new Border());
            Children.Add(head);

            Func<string, TextBlock, Slider, UIElement> row = (label, val, sl) =>
            {
                var d = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
                var l = new TextBlock { Text = label, Width = 70, Foreground = B("#8C84A0"), VerticalAlignment = VerticalAlignment.Center };
                val.Width = 70; val.VerticalAlignment = VerticalAlignment.Center; val.TextAlignment = TextAlignment.Right;
                DockPanel.SetDock(l, Dock.Left); DockPanel.SetDock(val, Dock.Right);
                d.Children.Add(l); d.Children.Add(val); d.Children.Add(sl); return d;
            };
            fT = new TextBlock(); gT = new TextBlock(); qT = new TextBlock();
            freq = new Slider { Style = (Style)FindResource("HFader"), Minimum = 0, Maximum = 1000, Foreground = Accent.Brush, VerticalAlignment = VerticalAlignment.Center };
            gain = new Slider { Style = (Style)FindResource("HFader"), Minimum = -12, Maximum = 12, Foreground = Accent.Brush, VerticalAlignment = VerticalAlignment.Center };
            q = new Slider { Style = (Style)FindResource("HFader"), Minimum = 0, Maximum = 1000, Foreground = Accent.Brush, VerticalAlignment = VerticalAlignment.Center };
            freq.ValueChanged += (o, e) => { if (loading || Cur() == null) return; Cur().Freq = RoundF(F(freq.Value / 1000 * W)); Redraw(); Labels(); Fire(); };
            gain.ValueChanged += (o, e) => { if (loading || Cur() == null) return; Cur().Gain = Math.Round(gain.Value * 2) / 2; Redraw(); Labels(); Fire(); };
            q.ValueChanged += (o, e) => { if (loading || Cur() == null) return; Cur().Q = Math.Round(0.3 * Math.Pow(10 / 0.3, q.Value / 1000) * 100) / 100; Redraw(); Labels(); Fire(); };
            Children.Add(row("Pitch", fT, freq)); Children.Add(row("Boost/cut", gT, gain)); Children.Add(row("Width (Q)", qT, q));
        }

        PBand Cur() { return sel >= 0 && sel < bands.Count ? bands[sel] : null; }
        void Fire() { if (Changed != null) Changed(); }
        public void Load(List<PBand> list) { bands = list ?? new List<PBand>(); sel = bands.Count > 0 ? Math.Min(Math.Max(sel, 0), bands.Count - 1) : -1; Redraw(); Sync(); }

        void OnMove(object o, MouseEventArgs e)
        {
            if (!dragging || Cur() == null) return;
            var p = e.GetPosition(plot); var b = Cur();
            b.Freq = RoundF(F(p.X));
            if (PEq.HasGain(b)) b.Gain = Math.Round(G(p.Y) * 2) / 2;
            Redraw(); Sync(); Fire();
        }

        void Redraw()
        {
            dyn.Children.Clear();
            // filled curve
            var pts = new PointCollection(); var fill = new PointCollection(); fill.Add(new Point(0, Y(0)));
            for (int i = 0; i <= 240; i++) { double x = W * i / 240; var pt = new Point(x, Y(PEq.Response(bands, F(x)))); pts.Add(pt); fill.Add(pt); }
            fill.Add(new Point(W, Y(0)));
            dyn.Children.Add(new Polygon { Points = fill, Fill = new SolidColorBrush(Color.FromArgb(40, Accent.Current.R, Accent.Current.G, Accent.Current.B)) });
            if (sel >= 0 && sel < bands.Count && bands[sel].On)   // the selected band on its own, faint
            {
                var one = new PointCollection(); var sb = bands[sel];
                for (int i = 0; i <= 160; i++) { double x = W * i / 160; one.Add(new Point(x, Y(PEq.Db(sb, F(x))))); }
                dyn.Children.Add(new Polyline { Points = one, Stroke = B(Colors[sel % Colors.Length]), StrokeThickness = 1, Opacity = 0.55, StrokeDashArray = new DoubleCollection { 3, 3 } });
            }
            dyn.Children.Add(new Polyline { Points = pts, Stroke = Accent.Brush, StrokeThickness = 2.2 });
            for (int i = 0; i < bands.Count; i++)
            {
                var b = bands[i]; int idx = i;
                double y = PEq.HasGain(b) ? Y(b.Gain) : Y(PEq.Response(bands, b.Freq));
                var dot = new Grid { Width = 18, Height = 18, Cursor = Cursors.SizeAll, ToolTip = PEq.TypeNames[Math.Max(0, Array.IndexOf(PEq.Types, b.Type))] + " " + FText(b.Freq) };
                dot.Children.Add(new Ellipse { Fill = b.On ? B(Colors[i % Colors.Length]) : B("#4A4258"), Stroke = i == sel ? Brushes.White : B("#1E1926"), StrokeThickness = i == sel ? 2 : 1.5 });
                dot.Children.Add(new TextBlock { Text = (i + 1).ToString(), FontSize = 10, FontWeight = FontWeights.Bold, Foreground = B("#1E1926"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false });
                dot.MouseLeftButtonDown += (o, e) =>
                {
                    sel = idx;
                    if (e.ClickCount == 2) { if (PEq.HasGain(bands[idx])) bands[idx].Gain = 0; Redraw(); Sync(); Fire(); e.Handled = true; return; }
                    dragging = true; plot.CaptureMouse(); Redraw(); Sync(); e.Handled = true;
                };
                Canvas.SetLeft(dot, X(b.Freq) - 9); Canvas.SetTop(dot, y - 9);
                dyn.Children.Add(dot);
            }
        }

        void Sync()
        {
            loading = true;
            var b = Cur();
            bool has = b != null;
            on.IsEnabled = type.IsEnabled = remove.IsEnabled = freq.IsEnabled = q.IsEnabled = has;
            gain.IsEnabled = has && PEq.HasGain(b);
            if (has)
            {
                title.Text = "Band " + (sel + 1);
                on.IsChecked = b.On; type.SelectedIndex = Math.Max(0, Array.IndexOf(PEq.Types, b.Type));
                freq.Value = X(b.Freq) / W * 1000; gain.Value = b.Gain; q.Value = Math.Log(b.Q / 0.3) / Math.Log(10 / 0.3) * 1000;
            }
            else title.Text = "No band";
            Labels();
            loading = false;
        }
        void Labels()
        {
            var b = Cur();
            fT.Text = b == null ? "" : FText(b.Freq);
            gT.Text = b == null || !PEq.HasGain(b) ? "-" : (b.Gain > 0 ? "+" : "") + b.Gain.ToString("0.#", CultureInfo.InvariantCulture) + " dB";
            qT.Text = b == null ? "" : b.Q.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}
