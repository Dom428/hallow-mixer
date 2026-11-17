// Hallow Mixer - local control API for Hallow Toolkit and its phone dashboard.
// Listens on 127.0.0.1 only (never the network). Every request needs the key in %APPDATA%\HallowMixer\api.json,
// which only your Windows account can read. Browsers can't use it (no CORS, Host checked, Origin refused).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Threading;

namespace HallowMixer
{
    public class MixerApi
    {
        TcpListener listener; Thread thread; volatile bool running;
        public int Port; public string Key;
        public Func<string> ShareJson;                 // GET /share -> the shareable part of your settings
        public Func<string, string> ApplyShare;        // POST /share-apply (JSON body) -> what changed
        readonly Func<string> state; readonly Func<string, Dictionary<string, string>, string> act; readonly Dispatcher ui;
        public static string FilePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"HallowMixer\api.json"); } }

        public MixerApi(Func<string> stateJson, Func<string, Dictionary<string, string>, string> action, Dispatcher dispatcher) { state = stateJson; act = action; ui = dispatcher; }

        public bool Start()
        {
            var b = new byte[24]; using (var r = RandomNumberGenerator.Create()) r.GetBytes(b);
            Key = BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();
            for (int p = 5191; p < 5200 && listener == null; p++)
            {
                try { var l = new TcpListener(IPAddress.Loopback, p); l.Start(); listener = l; Port = p; } catch { }
            }
            if (listener == null) return false;
            try { Directory.CreateDirectory(Path.GetDirectoryName(FilePath)); File.WriteAllText(FilePath, "{\"port\":" + Port + ",\"key\":\"" + Key + "\",\"pid\":" + System.Diagnostics.Process.GetCurrentProcess().Id + "}"); }
            catch { listener.Stop(); listener = null; return false; }
            running = true;
            thread = new Thread(Loop) { IsBackground = true, Name = "HallowMixer API" }; thread.Start();
            return true;
        }
        public void Stop()
        {
            running = false;
            try { if (listener != null) listener.Stop(); } catch { }
            listener = null;
            try { if (File.Exists(FilePath) && File.ReadAllText(FilePath).Contains(Key ?? "?")) File.Delete(FilePath); } catch { }
        }

        void Loop()
        {
            while (running)
            {
                TcpClient c;
                try { c = listener.AcceptTcpClient(); } catch { break; }
                try { Handle(c); } catch { } finally { try { c.Close(); } catch { } }
            }
        }

        static bool SameKey(string a, string b)   // constant-time compare
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int d = 0; for (int i = 0; i < a.Length; i++) d |= a[i] ^ b[i]; return d == 0;
        }

        void Handle(TcpClient c)
        {
            c.ReceiveTimeout = 2000; c.SendTimeout = 2000;
            var s = c.GetStream(); var buf = new byte[8192]; int n = 0, headEnd = -1;
            while (n < buf.Length)
            {
                int r = s.Read(buf, n, buf.Length - n); if (r <= 0) break; n += r;
                headEnd = Encoding.ASCII.GetString(buf, 0, n).IndexOf("\r\n\r\n"); if (headEnd >= 0) break;
            }
            var lines = Encoding.ASCII.GetString(buf, 0, headEnd >= 0 ? headEnd : n).Split(new[] { "\r\n" }, StringSplitOptions.None);
            var first = lines[0].Split(' ');
            var head = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in lines.Skip(1)) { int i = l.IndexOf(':'); if (i > 0) head[l.Substring(0, i).Trim()] = l.Substring(i + 1).Trim(); }
            string host; head.TryGetValue("Host", out host);
            string key; head.TryGetValue("X-Hallow-Key", out key);
            string reqBody = null; string lenText; int len;
            if (headEnd >= 0 && head.TryGetValue("Content-Length", out lenText) && int.TryParse(lenText, out len) && len > 0 && len <= 512 * 1024)
            {
                var data = new byte[len]; int have = Math.Min(len, n - (headEnd + 4));
                Array.Copy(buf, headEnd + 4, data, 0, have);
                while (have < len) { int r = s.Read(data, have, len - have); if (r <= 0) break; have += r; }
                if (have == len) reqBody = Encoding.UTF8.GetString(data);
            }
            int code = 200; string body;
            if (first.Length < 3 || (first[0] != "GET" && first[0] != "POST")) { code = 400; body = "{\"ok\":false,\"msg\":\"bad request\"}"; }
            else if (head.ContainsKey("Origin") || (host != "127.0.0.1:" + Port && host != "localhost:" + Port) || !SameKey(key, Key)) { code = 403; body = "{\"ok\":false,\"msg\":\"forbidden\"}"; }
            else
            {
                var url = first[1]; int qi = url.IndexOf('?');
                var path = qi < 0 ? url : url.Substring(0, qi);
                var q = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (qi >= 0) foreach (var kv in url.Substring(qi + 1).Split('&')) { var p = kv.Split(new[] { '=' }, 2); if (p[0].Length > 0) q[Uri.UnescapeDataString(p[0])] = p.Length > 1 ? Uri.UnescapeDataString(p[1].Replace('+', ' ')) : ""; }
                if (path == "/state") body = (string)ui.Invoke(state);
                else if (path == "/share" && ShareJson != null) body = (string)ui.Invoke(ShareJson);
                else if (path == "/share-apply" && ApplyShare != null)
                {
                    if (first[0] != "POST" || reqBody == null) { code = 400; body = "{\"ok\":false,\"msg\":\"send the setup as a POST body\"}"; }
                    else { var msg = (string)ui.Invoke(ApplyShare, reqBody); body = "{\"ok\":" + (msg.StartsWith("!") ? "false" : "true") + ",\"msg\":\"" + msg.TrimStart('!').Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}"; }
                }
                else
                {
                    var msg = (string)ui.Invoke(act, path, q);
                    if (msg == null) { code = 404; body = "{\"ok\":false,\"msg\":\"unknown command\"}"; }
                    else body = "{\"ok\":true,\"msg\":\"" + msg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
                }
            }
            var bytes = Encoding.UTF8.GetBytes(body);
            var reason = code == 200 ? "OK" : code == 403 ? "Forbidden" : code == 404 ? "Not Found" : "Bad Request";
            var h = Encoding.ASCII.GetBytes("HTTP/1.1 " + code + " " + reason + "\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n");
            s.Write(h, 0, h.Length); s.Write(bytes, 0, bytes.Length); s.Flush();
        }
    }
}
