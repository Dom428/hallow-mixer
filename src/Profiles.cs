// Hallow Mixer - profiles: whole mixer setups (levels, presets, app choices, EQ, switches) for different people
// or uses on one PC. Each one is a copy of settings.json in %APPDATA%\HallowMixer\profiles.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HallowMixer
{
    public static class Profiles
    {
        public static string Dir { get { return Path.Combine(Path.GetDirectoryName(Settings.FilePathPublic), "profiles"); } }
        static string FileOf(string name) { return Path.Combine(Dir, name + ".json"); }

        // Letters, numbers, spaces and - _ only, up to 30 characters. Null if nothing is left.
        public static string Clean(string name)
        {
            if (name == null) return null;
            var s = new string(name.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_').ToArray()).Trim();
            if (s.Length > 30) s = s.Substring(0, 30).Trim();
            return s.Length == 0 ? null : s;
        }

        // Every profile name, the current one included.
        public static List<string> List(string current)
        {
            var names = new List<string>();
            try { if (Directory.Exists(Dir)) names.AddRange(Directory.GetFiles(Dir, "*.json").Select(f => Path.GetFileNameWithoutExtension(f))); } catch { }
            if (!names.Contains(current, StringComparer.OrdinalIgnoreCase)) names.Add(current);
            return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // Keeps the current setup under its own name, then makes `target` the setup Hallow Mixer loads next.
        // newCopy: target doesn't exist yet - it starts as a copy of the current setup.
        public static void Switch(Settings cur, string target, bool newCopy)
        {
            Directory.CreateDirectory(Dir);
            var live = Settings.FilePathPublic;
            cur.Save();
            File.Copy(live, FileOf(cur.ProfileName), true);
            if (newCopy)
            {
                cur.ProfileName = target; cur.Save();
                File.Copy(live, FileOf(target), true);
            }
            else
            {
                if (!Settings.LooksValid(FileOf(target))) throw new InvalidOperationException("The " + target + " profile file is damaged.");
                File.Copy(FileOf(target), live, true);
            }
        }

        public static void Delete(string name)
        {
            var f = FileOf(name); if (File.Exists(f)) File.Delete(f);
        }
    }
}
