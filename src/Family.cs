// Hallow Mixer - Hallow family link (v1): a named pipe the other Hallow apps can ask, next to the local HTTP API.
//   Pipe: HallowFamily.Mixer.<Windows user name>, current user only (never reachable from the network).
//   The client writes one UTF-8 line "<command> [argument...]", the reply is one line: "ok", "ok <json>" or "error <message>".
//   ping   -> ok {"app":"Mixer","version":"3.14"}
//   status -> ok <the same state as the HTTP API's /state, with "app" and "rev"> (no keys or secrets)
//   HallowMixer.exe --cmd "<line>" sends a line to the running copy and prints the reply (exit 0 ok, 1 error, 2 not running).
using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HallowMixer
{
    public class FamilyPipe
    {
        public static string Name { get { return "HallowFamily.Mixer." + Environment.UserName; } }
        readonly Func<string, string> answer;   // command line -> reply line (called on a pipe thread)
        volatile bool running;

        public FamilyPipe(Func<string, string> handle) { answer = handle; }

        public void Start()
        {
            if (running) return;
            running = true;
            new Thread(Loop) { IsBackground = true, Name = "HallowFamily pipe" }.Start();
        }
        public void Stop() { running = false; }

        void Loop()
        {
            int fails = 0;
            while (running)
            {
                NamedPipeServerStream s = null;
                try
                {
                    var ps = new PipeSecurity();
                    ps.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));
                    s = new NamedPipeServerStream(Name, PipeDirection.InOut, 4, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, ps);
                    s.WaitForConnection();
                    fails = 0;
                    var conn = s; s = null;
                    new Thread(() => Serve(conn)) { IsBackground = true, Name = "HallowFamily request" }.Start();
                }
                catch
                {
                    if (s != null) try { s.Dispose(); } catch { }
                    if (++fails > 20) return;   // the name is taken or pipes don't work here - give up quietly
                    Thread.Sleep(1000);
                }
            }
        }

        void Serve(NamedPipeServerStream s)
        {
            try
            {
                var line = ReadLine(s, 3000);
                string reply;
                if (line == null) return;
                try { reply = answer(line.Trim()); } catch (Exception ex) { reply = "error " + ex.Message; }
                var b = Encoding.UTF8.GetBytes((reply ?? "error no answer").Replace("\r", " ").Replace("\n", " ") + "\n");
                s.Write(b, 0, b.Length); s.Flush();
                try { s.WaitForPipeDrain(); } catch { }
            }
            catch { }
            finally { try { s.Dispose(); } catch { } }
        }

        // One line (up to 8 KB), or null if nothing arrives in time.
        static string ReadLine(Stream s, int ms)
        {
            var buf = new byte[8192]; int n = 0; var until = DateTime.UtcNow.AddMilliseconds(ms);
            while (n < buf.Length)
            {
                var left = (int)(until - DateTime.UtcNow).TotalMilliseconds; if (left <= 0) return null;
                Task<int> t = s.ReadAsync(buf, n, buf.Length - n);
                if (!t.Wait(left)) return null;
                if (t.Result <= 0) break;
                n += t.Result;
                if (Array.IndexOf(buf, (byte)'\n', 0, n) >= 0) break;
            }
            var text = Encoding.UTF8.GetString(buf, 0, n);
            int i = text.IndexOf('\n'); return i >= 0 ? text.Substring(0, i) : text;
        }

        // ---- v3.15: ask another Hallow app (Hub) one line; null if it isn't running, doesn't answer in time or fails
        public static string Ask(string app, string line, int ms)
        {
            try
            {
                using (var c = new NamedPipeClientStream(".", "HallowFamily." + app + "." + Environment.UserName, PipeDirection.InOut, PipeOptions.Asynchronous))
                {
                    c.Connect(ms);
                    var b = Encoding.UTF8.GetBytes((line ?? "").Replace("\r", " ").Replace("\n", " ") + "\n");
                    c.Write(b, 0, b.Length); c.Flush();
                    return ReadLine(c, ms);
                }
            }
            catch { return null; }
        }

        // Writes a line to the console this was started from (window apps have none of their own).
        public static void Print(string text)
        {
            try
            {
                var h = GetStdHandle(-11);
                if (h == IntPtr.Zero || h == new IntPtr(-1)) AttachConsole(-1);
                var o = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
                o.WriteLine(text);
            }
            catch { }
        }

        // ---- --cmd "<line>": the command-line client
        [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);
        [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int which);
        public static int Send(string line)
        {
            string reply = null; int code;
            try
            {
                using (var c = new NamedPipeClientStream(".", Name, PipeDirection.InOut))
                {
                    c.Connect(1500);
                    var b = Encoding.UTF8.GetBytes((line ?? "").Replace("\r", " ").Replace("\n", " ") + "\n");
                    c.Write(b, 0, b.Length); c.Flush();
                    reply = ReadLine(c, 6000) ?? "error no answer";
                }
                code = reply == "ok" || reply.StartsWith("ok ") ? 0 : 1;
            }
            catch (TimeoutException) { reply = "error Hallow Mixer isn't running"; code = 2; }
            catch (IOException ex) { reply = "error " + ex.Message; code = 2; }
            catch (Exception ex) { reply = "error " + ex.Message; code = 1; }
            try
            {
                // a window app has no console of its own: use the one it was started from (if the output isn't redirected already)
                var h = GetStdHandle(-11);
                if (h == IntPtr.Zero || h == new IntPtr(-1)) AttachConsole(-1);
                var o = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
                o.WriteLine(reply);
            }
            catch { }
            return code;
        }
    }
}
