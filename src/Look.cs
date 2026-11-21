// Hallow Mixer - v3.25 light and dark mode (Hallow family spec). theme.json "mode": "dark" | "light" | "system" (written by
// Hallow Toolkit), overridden by Settings > Look (Follow Hallow / Light / Dark). Dark = the look Hallow Mixer always had.
// The palette lives in unfrozen shared brushes (code-built windows) and in app.Resources (Theme.xaml styles, DynamicResource),
// so a change recolours every open window at once, no restart.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace HallowMixer
{
    public static class Look
    {
        // ---- the switch (pure, unit-tested)
        // over: this app's own choice ("hallow" / "light" / "dark"); family: theme.json's mode; systemLight: Windows' app mode (null = not set)
        public static string Resolve(string over, string family, Func<bool?> systemLight)
        {
            var o = Norm(over);
            if (o == "light" || o == "dark") return o;
            var f = Norm(family);
            if (f == "light") return "light";
            if (f == "system") { bool? l = null; try { l = systemLight == null ? null : systemLight(); } catch { } return l == true ? "light" : "dark"; }
            return "dark";
        }
        public static string Norm(string s) { return (s ?? "").Trim().ToLowerInvariant(); }
        // theme.json's "mode" (trimmed, lower-case), or "dark" when it's missing or the file can't be read
        public static string ModeFrom(string json)
        {
            if (json == null) return "dark";
            var m = Regex.Match(json, "\"mode\"\\s*:\\s*\"([^\"]*)\"");
            if (!m.Success) return "dark";
            var v = Norm(m.Groups[1].Value);
            return v == "light" || v == "system" ? v : "dark";
        }
        public static string ReadMode() { return ModeFrom(Accent.ReadText()); }
        public static Func<bool?> SystemLightOverride;   // tests
        public static bool? SystemLight()
        {
            if (SystemLightOverride != null) return SystemLightOverride();
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var v = k == null ? null : k.GetValue("AppsUseLightTheme");
                    if (v is int) return (int)v == 1;
                    return null;
                }
            }
            catch { return null; }
        }

        // ---- colour maths (WCAG)
        public static double Lum(Color c)
        {
            Func<byte, double> ch = b => { double v = b / 255.0; return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); };
            return 0.2126 * ch(c.R) + 0.7152 * ch(c.G) + 0.0722 * ch(c.B);
        }
        public static double Contrast(Color a, Color b) { double x = Lum(a), y = Lum(b); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05); }
        static void ToHsl(Color c, out double h, out double s, out double l)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0, max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2; h = 0; s = 0;
            if (max == min) return;
            double d = max - min; s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0); else if (max == g) h = (b - r) / d + 2; else h = (r - g) / d + 4;
            h /= 6;
        }
        static Color FromHsl(double h, double s, double l)
        {
            Func<double, double, double, double> hue = (p, q, t) => { if (t < 0) t += 1; if (t > 1) t -= 1; if (t < 1 / 6.0) return p + (q - p) * 6 * t; if (t < 0.5) return q; if (t < 2 / 3.0) return p + (q - p) * (2 / 3.0 - t) * 6; return p; };
            double r, g, b;
            if (s == 0) r = g = b = l;
            else { double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q; r = hue(p, q, h + 1 / 3.0); g = hue(p, q, h); b = hue(p, q, h - 1 / 3.0); }
            return Color.FromRgb((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
        }
        // Keeps hue and saturation, lowers lightness in 0.01 steps until the colour has min:1 contrast with bg.
        public static Color Darken(Color c, Color bg, double min)
        {
            double h, s, l; ToHsl(c, out h, out s, out l);
            var x = c;
            while (Contrast(x, bg) < min && l > 0) { l = Math.Max(0, l - 0.01); x = FromHsl(h, s, l); }
            return x;
        }
        public static readonly Color White = Color.FromRgb(0xFF, 0xFF, 0xFF), Ink0 = Color.FromRgb(0x1E, 0x1A, 0x26);
        // v3.26 the darkest light surface text can sit on (Panel 2 / hover): inks are darkened against it, so they pass on #F6F4FA and white too
        public static readonly Color Surface2 = Color.FromRgb(0xEE, 0xEA, 0xF5);
        // the accent (or a channel colour) for text, thin lines and icons: as it is on dark, darkened to 4.5:1 on #EEEAF5 in light mode
        public static Color InkOf(Color c) { return IsLight ? TextAccent(c) : c; }
        public static Color TextAccent(Color c) { return Darken(c, Surface2, 4.5); }
        // text on a filled colour in light mode: #1E1A26 or white, whichever reads better
        public static Color TextOnFill(Color c) { return Contrast(Ink0, c) >= Contrast(White, c) ? Ink0 : White; }
        // v3.26 the light-mode fill for accent buttons / pills (as Hallow Toolkit 5.16): the real accent if its text colour reaches 4.5:1
        // on it, else the accent darkened to 4.5:1 on white (e.g. violet #8B5CF6), with white text
        public static Color FillOf(Color c) { return Contrast(c, TextOnFill(c)) >= 4.5 ? c : Darken(c, White, 4.5); }
        public static Color Scale(Color c, double k) { return Color.FromRgb((byte)(c.R * k), (byte)(c.G * k), (byte)(c.B * k)); }

        // ---- palette: key -> { dark, light }. Keys are the resource names Theme.xaml uses.
        static Color C(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
        public static readonly Dictionary<string, string[]> Palette = new Dictionary<string, string[]>
        {
            { "Bg",         new[] { "#16121C", "#F6F4FA" } },
            { "Panel",      new[] { "#211B2A", "#FFFFFF" } },
            { "PanelHi",    new[] { "#2E2639", "#EEEAF5" } },
            { "Line",       new[] { "#3A3248", "#DAD3E6" } },
            { "Text",       new[] { "#ECE8F4", "#1E1A26" } },
            { "Muted",      new[] { "#8F88A0", "#6B6478" } },
            { "Lilac",      new[] { "#A98BE0", "#7A5CC0" } },
            { "Green",      new[] { "#6CCB7A", "#1E8E4F" } },
            { "Amber",      new[] { "#E8C547", "#A66A00" } },
            { "Red",        new[] { "#C8414B", "#C2333F" } },
            { "GreenText",  new[] { "#6CCB7A", "#1B7A44" } },   // v3.26 green / amber as small text (the icon values are just under 4.5:1)
            { "AmberText",  new[] { "#E8C547", "#8F5B00" } },
            { "RedLine",    new[] { "#E05A64", "#A82B36" } },
            { "OnRed",      new[] { "#ECE8F4", "#FFFFFF" } },   // text/icon on a red (muted) button
            { "Field",      new[] { "#241E2E", "#FFFFFF" } },   // settings flyout, text boxes, combo pop-ups
            { "BtnBg",      new[] { "#2A2333", "#EEEAF5" } },
            { "BtnLineHi",  new[] { "#55496A", "#B9AECB" } },
            { "Hover",      new[] { "#FFFFFF", "#2A1F3D" } },   // the see-through hover/press overlay on buttons
            { "Track",      new[] { "#3A3248", "#DAD3E6" } },   // slider, empty part
            { "MiniTrack",  new[] { "#352D42", "#E1DBEB" } },
            { "Knob",       new[] { "#ECE8F4", "#FFFFFF" } },
            { "KnobHi",     new[] { "#FFFFFF", "#F6F4FA" } },
            { "KnobRing",   new[] { "#16121C", "#8A809C" } },
            { "MiniKnob",   new[] { "#DCD7E6", "#FFFFFF" } },
            { "ScrollBack", new[] { "#14FFFFFF", "#12000000" } },
            { "Thumb",      new[] { "#4A4058", "#C6BDD5" } },
            { "ThumbHi",    new[] { "#6A5E7C", "#A99EBC" } },
            { "ThumbDrag",  new[] { "#8F88A0", "#8A809C" } },
            { "SwitchOff",  new[] { "#58566A", "#8E85A0" } },
            { "SwitchKnob", new[] { "#CDCAD6", "#FFFFFF" } },
            { "SwitchOn",   new[] { "#6ED28C", "#1E8E4F" } },
            { "ComboHi",    new[] { "#3A3248", "#E6E1EF" } },
            { "Card",       new[] { "#271F31", "#FFFFFF" } },   // an app inside a channel
            { "CardHi",     new[] { "#2E2539", "#F3F0F8" } },
            { "CardLine",   new[] { "#30283C", "#E3DDEC" } },
            { "BarLine",    new[] { "#2C2538", "#E3DDEC" } },
            { "Dim",        new[] { "#5A5168", "#C4BDD0" } },   // a muted app's slider
            { "MeterBack",  new[] { "#2A2333", "#ECE8F3" } },
            { "Plot",       new[] { "#1E1926", "#FFFFFF" } },   // the parametric EQ's graph
            { "PlotGrid",   new[] { "#2E2838", "#ECE8F3" } },
            { "PlotZero",   new[] { "#4A4258", "#C6BDD5" } },
            { "PlotLabel",  new[] { "#6E6680", "#6B6478" } },
            { "Hint",       new[] { "#8C84A0", "#6B6478" } },
            { "MiniShell",  new[] { "#F01C1724", "#F5FFFFFF" } },
        };
        static readonly Dictionary<string, SolidColorBrush> shared = new Dictionary<string, SolidColorBrush>();
        static Look() { foreach (var k in Palette) shared[k.Key] = new SolidColorBrush(C(k.Value[0])); }
        public static Color Col(string key) { return C(Palette[key][IsLight ? 1 : 0]); }
        // the shared (never frozen) brush for a palette key
        public static SolidColorBrush Get(string key) { return shared[key]; }
        public static SolidColorBrush Bg { get { return shared["Bg"]; } }
        public static SolidColorBrush Panel { get { return shared["Panel"]; } }
        public static SolidColorBrush PanelHi { get { return shared["PanelHi"]; } }
        public static SolidColorBrush Line { get { return shared["Line"]; } }
        public static SolidColorBrush Text { get { return shared["Text"]; } }
        public static SolidColorBrush Muted { get { return shared["Muted"]; } }
        public static SolidColorBrush Lilac { get { return shared["Lilac"]; } }
        public static SolidColorBrush Green { get { return shared["Green"]; } }
        public static SolidColorBrush Field { get { return shared["Field"]; } }

        public static bool IsLight { get; private set; }
        public static string Mode { get { return IsLight ? "light" : "dark"; } }
        public static event Action Changed;

        // ---- live brushes: anything computed from the palette (channel tints, inks) repaints on every change.
        // Held weakly, so cards that are rebuilt every few seconds don't pile up.
        static readonly List<KeyValuePair<WeakReference, Action<object>>> live = new List<KeyValuePair<WeakReference, Action<object>>>();
        // v3.26 green / amber status TEXT: the family text values in light mode (#1B7A44 / #8F5B00); Green / Amber stay for icons, dots, fills
        public static SolidColorBrush GreenText { get { return shared["GreenText"]; } }
        public static SolidColorBrush AmberText { get { return shared["AmberText"]; } }
        public static T Live<T>(T target, Action<T> paint) where T : class
        {
            paint(target);
            if (live.Count > 512) live.RemoveAll(x => !x.Key.IsAlive);
            live.Add(new KeyValuePair<WeakReference, Action<object>>(new WeakReference(target), o => paint((T)o)));
            return target;
        }
        // the channel colour mixed into the panel colour (a: 0 = panel, 1 = the colour)
        public static Color Mix(Color c, double a) { var p = Col("Panel"); return Color.FromRgb((byte)(p.R + (c.R - p.R) * a), (byte)(p.G + (c.G - p.G) * a), (byte)(p.B + (c.B - p.B) * a)); }
        public static SolidColorBrush Tint(Color c, double a) { return Live(new SolidColorBrush(), b => b.Color = Mix(c, a)); }
        public static SolidColorBrush Ink(Color c) { return Live(new SolidColorBrush(), b => b.Color = InkOf(c)); }
        public static SolidColorBrush Pick(string dark, string light) { return Live(new SolidColorBrush(), b => b.Color = C(IsLight ? light : dark)); }

        // ---- install, follow, apply
        static Application app; static Func<string> overrideOf; static bool applied;
        public static void Install(Application a, Func<string> over)
        {
            app = a; overrideOf = over;
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, e) => TitleBar(s as Window)));
            Update();
        }
        public static void SetOverride(Func<string> over) { overrideOf = over; Update(); }
        // reads theme.json + the override (+ Windows' setting for "system") and applies the result if it changed
        public static void Update()
        {
            string o = null; try { o = overrideOf == null ? null : overrideOf(); } catch { }
            Apply(Resolve(o, ReadMode(), SystemLight) == "light");
        }
        public static void Apply(bool light)
        {
            if (applied && light == IsLight) return;
            applied = true; IsLight = light;
            foreach (var k in Palette) shared[k.Key].Color = Col(k.Key);
            if (app != null)
            {
                foreach (var k in Palette) app.Resources[k.Key] = new SolidColorBrush(Col(k.Key));
                // game / chat balance: Game's orange, the empty track in the middle, Chat's lilac (channel colours stay)
                var bal = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
                bal.GradientStops.Add(new GradientStop(C("#F07820"), 0)); bal.GradientStops.Add(new GradientStop(Col("Track"), 0.5)); bal.GradientStops.Add(new GradientStop(C("#A98BE0"), 1));
                app.Resources["BalanceBar"] = bal;
            }
            foreach (var x in live.ToList()) { var t = x.Key.Target; if (t != null) try { x.Value(t); } catch { } }
            live.RemoveAll(x => !x.Key.IsAlive);
            Accent.Repaint();
            if (app != null) foreach (Window w in app.Windows) TitleBar(w);
            var h = Changed; if (h != null) try { h(); } catch { }
        }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
        public static void TitleBar(Window w)
        {
            if (w == null) return;
            try { var h = new WindowInteropHelper(w).Handle; if (h == IntPtr.Zero) return; int dark = IsLight ? 0 : 1; DwmSetWindowAttribute(h, 20, ref dark, 4); } catch { }
        }
        public static void TitleBar(IntPtr h) { try { int dark = IsLight ? 0 : 1; DwmSetWindowAttribute(h, 20, ref dark, 4); } catch { } }
    }
}
