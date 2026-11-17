// Hallow Mixer - OBS link through obs-websocket v5 (built into OBS 28 and newer: Tools > WebSocket Server Settings).
// Lets Hallow Mixer run a separate stream mix (each channel's level in OBS) and switch presets when you change scenes.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace HallowMixer
{
    public class ObsException : Exception { public ObsException(string m) : base(m) { } }

    public class ObsLink : IDisposable
    {
        ClientWebSocket ws;
        CancellationTokenSource cts;
        readonly JavaScriptSerializer json = new JavaScriptSerializer();
        readonly Dictionary<string, TaskCompletionSource<Dictionary<string, object>>> pending = new Dictionary<string, TaskCompletionSource<Dictionary<string, object>>>();
        int nextId;
        public bool Connected { get; private set; }
        public string LastError;
        public string ObsVersion;
        public event Action<string> SceneChanged;      // program scene name
        public event Action<string> Disconnected;      // reason

        // password: stored encrypted for this Windows user only (DPAPI) - never in plain text
        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser));
        }
        public static string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return "";
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(stored), null, DataProtectionScope.CurrentUser)); } catch { return ""; }
        }
        static string B64Sha(string s) { using (var h = SHA256.Create()) return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(s))); }
        public static string AuthString(string password, string salt, string challenge) { return B64Sha(B64Sha(password + salt) + challenge); }

        public async Task<bool> Connect(string host, int port, string password)
        {
            Close();
            ws = new ClientWebSocket(); cts = new CancellationTokenSource();
            try
            {
                var conn = ws.ConnectAsync(new Uri("ws://" + host + ":" + port), cts.Token);
                if (await Task.WhenAny(conn, Task.Delay(4000)) != conn) throw new ObsException("OBS didn't answer - is OBS open, with its WebSocket server turned on?");
                await conn;
                var hello = await ReadMessage(5000);
                if (hello == null || Op(hello) != 0) throw new ObsException("That isn't OBS's WebSocket server.");
                var hd = D(hello);
                ObsVersion = hd.ContainsKey("obsWebSocketVersion") ? Convert.ToString(hd["obsWebSocketVersion"]) : "?";
                var ident = new Dictionary<string, object> { { "rpcVersion", 1 }, { "eventSubscriptions", 4 } };   // 4 = scene events
                if (hd.ContainsKey("authentication"))
                {
                    if (string.IsNullOrEmpty(password)) throw new ObsException("OBS needs the WebSocket password (OBS > Tools > WebSocket Server Settings > Show Connect Info).");
                    var a = (Dictionary<string, object>)hd["authentication"];
                    ident["authentication"] = AuthString(password, (string)a["salt"], (string)a["challenge"]);
                }
                await Send(1, ident);
                var ok = await ReadMessage(5000);
                if (ok == null || Op(ok) != 2) throw new ObsException(ws.CloseStatus == (WebSocketCloseStatus)4009 ? "Wrong OBS WebSocket password." : "OBS refused the connection" + (ws.CloseStatusDescription != null ? ": " + ws.CloseStatusDescription : "."));
                Connected = true; LastError = null;
                var loop = ReceiveLoop();
                return true;
            }
            catch (ObsException e) { LastError = e.Message; }
            catch (Exception e)
            {
                LastError = ws != null && ws.CloseStatus == (WebSocketCloseStatus)4009 ? "Wrong OBS WebSocket password."
                    : "Couldn't reach OBS (" + (e.InnerException ?? e).Message.TrimEnd('.') + "). Is OBS open, with Tools > WebSocket Server Settings > Enable turned on?";
            }
            Close();
            return false;
        }

        static int Op(Dictionary<string, object> m) { return Convert.ToInt32(m["op"]); }
        static Dictionary<string, object> D(Dictionary<string, object> m) { object d; return m.TryGetValue("d", out d) && d is Dictionary<string, object> ? (Dictionary<string, object>)d : new Dictionary<string, object>(); }

        async Task Send(int op, object d)
        {
            var bytes = Encoding.UTF8.GetBytes(json.Serialize(new Dictionary<string, object> { { "op", op }, { "d", d } }));
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
        }
        async Task<Dictionary<string, object>> ReadMessage(int timeoutMs)
        {
            var buf = new byte[16384]; var all = new List<byte>();
            while (true)
            {
                var t = ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token);
                if (timeoutMs > 0 && await Task.WhenAny(t, Task.Delay(timeoutMs)) != t) return null;
                var r = await t;
                if (r.MessageType == WebSocketMessageType.Close) return null;
                all.AddRange(buf.Take(r.Count));
                if (r.EndOfMessage) break;
            }
            return json.DeserializeObject(Encoding.UTF8.GetString(all.ToArray())) as Dictionary<string, object>;
        }
        async Task ReceiveLoop()
        {
            string why = "OBS closed the connection.";
            try
            {
                while (ws != null && ws.State == WebSocketState.Open)
                {
                    var m = await ReadMessage(0);
                    if (m == null) break;
                    var d = D(m);
                    if (Op(m) == 7)
                    {
                        var id = Convert.ToString(d["requestId"]);
                        TaskCompletionSource<Dictionary<string, object>> tcs;
                        lock (pending) { if (pending.TryGetValue(id, out tcs)) pending.Remove(id); }
                        if (tcs != null) tcs.TrySetResult(d);
                    }
                    else if (Op(m) == 5 && Convert.ToString(d["eventType"]) == "CurrentProgramSceneChanged" && SceneChanged != null)
                    {
                        var ed = d.ContainsKey("eventData") ? d["eventData"] as Dictionary<string, object> : null;
                        if (ed != null && ed.ContainsKey("sceneName")) SceneChanged(Convert.ToString(ed["sceneName"]));
                    }
                }
            }
            catch (Exception e) { why = "Lost the connection to OBS (" + e.Message.TrimEnd('.') + ")."; }
            bool was = Connected;
            Close();
            if (was && Disconnected != null) Disconnected(why);
        }

        // One request to OBS. Returns responseData (empty if none); throws ObsException if OBS says no.
        public async Task<Dictionary<string, object>> Request(string type, Dictionary<string, object> data)
        {
            if (!Connected) throw new ObsException("Not connected to OBS.");
            var id = (++nextId).ToString();
            var tcs = new TaskCompletionSource<Dictionary<string, object>>(TaskCreationOptions.RunContinuationsAsynchronously);   // never run callers on the receive loop
            lock (pending) pending[id] = tcs;
            var d = new Dictionary<string, object> { { "requestType", type }, { "requestId", id } };
            if (data != null) d["requestData"] = data;
            await Send(6, d);
            if (await Task.WhenAny(tcs.Task, Task.Delay(5000)) != tcs.Task) { lock (pending) pending.Remove(id); throw new ObsException("OBS didn't answer " + type + "."); }
            var res = tcs.Task.Result;
            var st = res["requestStatus"] as Dictionary<string, object>;
            if (st == null || !(st["result"] is bool) || !(bool)st["result"])
                throw new ObsException(type + " failed" + (st != null && st.ContainsKey("comment") ? ": " + st["comment"] : "."));
            return res.ContainsKey("responseData") ? (res["responseData"] as Dictionary<string, object> ?? new Dictionary<string, object>()) : new Dictionary<string, object>();
        }
        public static Dictionary<string, object> Args(params object[] kv)
        {
            var d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }

        // OBS sources that carry sound (desktop audio, mics, app capture, media, browser...)
        static readonly string[] AudioKinds = { "wasapi_output_capture", "wasapi_input_capture", "wasapi_process_output_capture", "ffmpeg_source", "vlc_source", "browser_source", "game_capture", "dshow_input", "coreaudio_input_capture", "pulse_input_capture", "pulse_output_capture" };
        public async Task<List<string>> AudioInputs()
        {
            var r = await Request("GetInputList", null);
            var res = new List<string>();
            foreach (Dictionary<string, object> i in (object[])r["inputs"])
            {
                var kind = Convert.ToString(i.ContainsKey("unversionedInputKind") ? i["unversionedInputKind"] : i["inputKind"]);
                if (AudioKinds.Contains(kind)) res.Add(Convert.ToString(i["inputName"]));
            }
            return res;
        }
        public async Task<List<string>> Scenes()
        {
            var r = await Request("GetSceneList", null);
            return ((object[])r["scenes"]).Cast<Dictionary<string, object>>().Select(s => Convert.ToString(s["sceneName"])).Reverse().ToList();   // OBS lists them bottom-up
        }
        public async Task<string> CurrentScene()
        {
            var r = await Request("GetCurrentProgramScene", null);
            return Convert.ToString(r.ContainsKey("currentProgramSceneName") ? r["currentProgramSceneName"] : r["sceneName"]);
        }

        public void Close()
        {
            Connected = false;
            lock (pending) { foreach (var t in pending.Values) t.TrySetCanceled(); pending.Clear(); }
            try { if (cts != null) cts.Cancel(); } catch { }
            try { if (ws != null) ws.Abort(); } catch { }
            try { if (ws != null) ws.Dispose(); } catch { }
            ws = null;
        }
        public void Dispose() { Close(); }
    }

    // The stream mix: each Hallow Mixer channel drives the OBS sources linked to it.
    public static class StreamMix
    {
        public static int Level(Settings s, string ch) { int v; return s.StreamLevel.TryGetValue(ch, out v) ? v : 100; }
        public static bool OnStream(Settings s, string ch) { return !s.StreamOff.Contains(ch); }
        public static List<string> Links(Settings s, string ch) { List<string> l; return s.StreamLinks.TryGetValue(ch, out l) ? l : new List<string>(); }

        // Pushes every channel's stream level and on/off to its OBS sources. Returns how many sources were set.
        public static async Task<int> Apply(ObsLink obs, Settings s, string onlyChannel)
        {
            int n = 0;
            foreach (var ch in s.StreamLinks.Keys.ToList())
            {
                if (onlyChannel != null && ch != onlyChannel) continue;
                double mul = Level(s, ch) / 100.0; bool mute = !OnStream(s, ch);
                foreach (var input in Links(s, ch).ToList())
                {
                    try
                    {
                        await obs.Request("SetInputVolume", ObsLink.Args("inputName", input, "inputVolumeMul", mul));
                        await obs.Request("SetInputMute", ObsLink.Args("inputName", input, "inputMuted", mute));
                        n++;
                    }
                    catch (ObsException) { }   // source renamed or deleted in OBS - skipped
                }
            }
            return n;
        }

        // Set up OBS for me: one "Application Audio Capture" source per app in Game/Chat/Music/Media, in the current scene, linked to its channel.
        public static async Task<string> AutoSetup(ObsLink obs, Engine eng)
        {
            var scene = await obs.CurrentScene();
            var existing = new HashSet<string>(await obs.AudioInputs(), StringComparer.OrdinalIgnoreCase);
            int made = 0, linked = 0; var skipped = new List<string>();
            foreach (var a in eng.Apps.Where(x => x.Key != "system" && x.Channel != "other" && x.ExePath != null))
            {
                string name = "HM " + Engine.ChannelNames[a.Channel] + " - " + a.Name;
                string exe = System.IO.Path.GetFileName(a.ExePath);
                if (!existing.Contains(name))
                {
                    try
                    {
                        await obs.Request("CreateInput", ObsLink.Args("sceneName", scene, "inputName", name, "inputKind", "wasapi_process_output_capture", "sceneItemEnabled", true));
                        // pick this app's window in the new source (OBS lists them as "title:class:exe")
                        var items = await obs.Request("GetInputPropertiesListPropertyItems", ObsLink.Args("inputName", name, "propertyName", "window"));
                        var match = ((object[])items["propertyItems"]).Cast<Dictionary<string, object>>().Select(i => Convert.ToString(i["itemValue"]))
                            .FirstOrDefault(v => v.EndsWith(":" + exe, StringComparison.OrdinalIgnoreCase));
                        if (match == null) { skipped.Add(a.Name + " (no window)"); await obs.Request("RemoveInput", ObsLink.Args("inputName", name)); continue; }
                        await obs.Request("SetInputSettings", ObsLink.Args("inputName", name, "inputSettings", ObsLink.Args("window", match, "priority", 2), "overlay", true));
                        made++;
                    }
                    catch (ObsException e) { skipped.Add(a.Name + " (" + e.Message + ")"); continue; }
                }
                List<string> l;
                if (!eng.S.StreamLinks.TryGetValue(a.Channel, out l)) { l = new List<string>(); eng.S.StreamLinks[a.Channel] = l; }
                if (!l.Contains(name)) { l.Add(name); linked++; }
            }
            eng.S.Save();
            await Apply(obs, eng.S, null);
            var msg = made == 0 && linked == 0 ? "Nothing new to set up - start your game, Discord and music first, then try again."
                : "Made " + made + " OBS source(s) in \"" + scene + "\" and linked " + linked + " to their channels.";
            if (skipped.Count > 0) msg += " Skipped: " + string.Join(", ", skipped.Take(4)) + (skipped.Count > 4 ? "..." : "") + ".";
            return msg;
        }
    }
}
