using MoonSharp.Interpreter;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilityBelt.Tools;
using WebSocketSharp;

namespace UtilityBelt.Lib.ScriptInterface {
    internal class RemoteScript : IDisposable {
        private static string WEBSOCKET_SERVER = "ws://localhost:8181/acclient-server";
        private static uint _NextId = 1;
        private WebSocket ws = null;
        private bool didDispose = false;
        private bool isSelfStopping = false;

        public uint ScriptId { get; }
        public string ScriptName => $"RemoteScript{ScriptId}";
        public string RemoteClientId { get; private set; }
        public UBScript.UBScript Script { get; private set; }

        public event EventHandler<EventArgs> OnClosed;

        public RemoteScript(string remoteClientId) {
            ScriptId = _NextId++;
            RemoteClientId = remoteClientId;

            Logger.WriteToChat($"Attempting to start script `{ScriptName}` from remote UBWebIDE instance `{RemoteClientId}`");

            UBLoader.FilterCore.Scripts.OnScriptStopped += Scripts_OnScriptStopped;

            Script = UBLoader.FilterCore.Scripts.StartScript(ScriptName, false);
            Script.OnLogText += Script_OnLogText;

            TryConnect();
        }

        private void Scripts_OnScriptStopped(object sender, UBScript.Events.ScriptEventArgs e) {
            if (!isSelfStopping && e.Script.Name == ScriptName) {
                Dispose();
            }
        }

        private void TryConnect() {
            ws = new WebSocket(WEBSOCKET_SERVER);

            ws.OnClose += Ws_OnClose;
            ws.OnMessage += Ws_OnMessage;
            ws.OnOpen += Ws_OnOpen;

            ws.Connect();
        }

        private void Ws_OnOpen(object sender, EventArgs e) {
            ws.Send((new JObject {
                { "command", "init" },
                { "clientId", RemoteClientId },
                { "type", "acclient" },
                { "name", $"{UBLoader.FilterCore.Scripts.GameState.ServerName}//{UBLoader.FilterCore.Scripts.GameState.Character.Weenie.Name}//{ScriptName}" }
            }).ToString());
        }

        private void Ws_OnMessage(object sender, MessageEventArgs e) {
            try {
                JObject o = JObject.Parse(e.Data);

                var command = (string)o["command"];
                switch (command) {
                    case "init":
                        break;

                    case "error":
                        //Logger.Error($"{ScriptName} WebSocket error: {(string)o["error"]}");
                        break;

                    case "runtext":
                        if (Script != null) {
                            Script.OnLogText -= Script_OnLogText;
                            if (UBLoader.FilterCore.Scripts.GetScript(ScriptName) != null) {
                                isSelfStopping = true;
                                UBLoader.FilterCore.Scripts.StopScript(ScriptName);
                                isSelfStopping = false;
                            }
                        }
                        Script = UBLoader.FilterCore.Scripts.StartScript(ScriptName, false);
                        Script.OnLogText += Script_OnLogText;
                        var text = (string)o["text"];
                        var watch = new System.Diagnostics.Stopwatch();
                        watch.Start();
                        var res = Script.RunText(text);
                        Logger.WriteToChat($"res is: {(res== null ? "null" : res.ToString())} // ws is {(ws == null ? "null" : ws.ToString())}");
                        watch.Stop();
                        ws.Send((new JObject {
                            { "command", "logs" },
                            { "logs", $"Result: {Scripts.PrettyPrint(res)} ({1000.0 * (double)watch.ElapsedTicks / Stopwatch.Frequency:N3}ms)" }
                        }).ToString());
                        break;

                    default:
                        Logger.Error($"unhandled websocket message: {command} // {e.Data}");
                        break;
                }
            }
            catch (Exception ex) {
                Logger.LogException(ex); 
            }
        }

        private void Script_OnLogText(object sender, UBScript.UBScript.LogEventArgs e) {
            try {
                ws?.Send((new JObject {
                    { "command", "logs" },
                    { "logs", e.Text }
                }).ToString());
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Ws_OnClose(object sender, CloseEventArgs e) {
            Logger.WriteToChat($"Lost connection to {ScriptName}");
            Dispose();
        }

        public void Dispose() {
            if (didDispose) return;
            didDispose = true;

            UBLoader.FilterCore.Scripts.OnScriptStopped -= Scripts_OnScriptStopped;
            OnClosed?.Invoke(this, EventArgs.Empty);

            ws?.Close();
            ws = null;

            if (UBLoader.FilterCore.Scripts.GetScript(ScriptName) != null) {
                UBLoader.FilterCore.Scripts.StopScript(ScriptName);
            }
        }
    }
}
